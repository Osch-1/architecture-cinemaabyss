using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Configuration from environment
var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
var monolithUrl = (Environment.GetEnvironmentVariable("MONOLITH_URL") ?? "http://monolith:8080").TrimEnd('/');
var moviesServiceUrl = (Environment.GetEnvironmentVariable("MOVIES_SERVICE_URL") ?? "http://movies-service:8081").TrimEnd('/');
var eventsServiceUrl = (Environment.GetEnvironmentVariable("EVENTS_SERVICE_URL") ?? "http://events-service:8082").TrimEnd('/');
var gradualMigration = string.Equals(Environment.GetEnvironmentVariable("GRADUAL_MIGRATION"), "true", StringComparison.OrdinalIgnoreCase);
var migrationPercent = int.TryParse(Environment.GetEnvironmentVariable("MOVIES_MIGRATION_PERCENT"), out var pct) ? Math.Clamp(pct, 0, 100) : 0;

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddHttpClient("proxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
var logger = app.Logger;

logger.LogInformation("Proxy service starting on port {Port}", port);
logger.LogInformation("Monolith:       {MonolithUrl}", monolithUrl);
logger.LogInformation("Movies service: {MoviesServiceUrl}", moviesServiceUrl);
logger.LogInformation("Events service: {EventsServiceUrl}", eventsServiceUrl);
logger.LogInformation("Gradual migration: {GradualMigration}, percent: {Percent}%", gradualMigration, migrationPercent);

// Hop-by-hop headers that should not be forwarded
var hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Connection", "Keep-Alive", "Transfer-Encoding", "TE",
    "Trailer", "Upgrade", "Proxy-Authorization", "Proxy-Authenticate"
};

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Map("/{**path}", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    var queryString = ctx.Request.QueryString.Value ?? "";

    // Strangler Fig routing logic
    string targetBaseUrl;

    if (path.StartsWith("/api/movies", StringComparison.OrdinalIgnoreCase))
    {
        if (gradualMigration)
        {
            var roll = Random.Shared.Next(100);
            var routeToMicroservice = roll < migrationPercent;
            targetBaseUrl = routeToMicroservice ? moviesServiceUrl : monolithUrl;
            logger.LogInformation(
                "[movies] rolled={Roll} threshold={Threshold} -> {Target}",
                roll, migrationPercent, routeToMicroservice ? "movies-service" : "monolith");
        }
        else
        {
            targetBaseUrl = monolithUrl;
        }
    }
    else if (path.StartsWith("/api/events", StringComparison.OrdinalIgnoreCase))
    {
        targetBaseUrl = eventsServiceUrl;
    }
    else
    {
        targetBaseUrl = monolithUrl;
    }

    var targetUri = targetBaseUrl + path + queryString;
    logger.LogDebug("Forwarding {Method} {Path} -> {Target}", ctx.Request.Method, path, targetUri);

    using var requestMessage = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetUri);

    // Forward request headers (skip hop-by-hop and Host)
    foreach (var header in ctx.Request.Headers)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) || hopByHopHeaders.Contains(header.Key))
            continue;

        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    // Forward body for write methods
    if (ctx.Request.Method == HttpMethods.Post
        || ctx.Request.Method == HttpMethods.Put
        || ctx.Request.Method == HttpMethods.Patch)
    {
        var bodyContent = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType is { } contentType)
            bodyContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (ctx.Request.ContentLength.HasValue)
            bodyContent.Headers.ContentLength = ctx.Request.ContentLength.Value;
        requestMessage.Content = bodyContent;
    }

    try
    {
        var client = factory.CreateClient("proxy");
        using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        ctx.Response.StatusCode = (int)response.StatusCode;

        foreach (var header in response.Headers)
        {
            if (!hopByHopHeaders.Contains(header.Key))
                ctx.Response.Headers[header.Key] = header.Value.ToArray();
        }
        foreach (var header in response.Content.Headers)
            ctx.Response.Headers[header.Key] = header.Value.ToArray();

        // Remove Transfer-Encoding — ASP.NET manages chunking itself
        ctx.Response.Headers.Remove("Transfer-Encoding");

        await response.Content.CopyToAsync(ctx.Response.Body);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Upstream error forwarding to {Target}", targetUri);
        ctx.Response.StatusCode = 502;
        await ctx.Response.WriteAsJsonAsync(new { error = "Bad Gateway", message = ex.Message });
    }
    catch (TaskCanceledException)
    {
        logger.LogError("Timeout forwarding to {Target}", targetUri);
        ctx.Response.StatusCode = 504;
        await ctx.Response.WriteAsJsonAsync(new { error = "Gateway Timeout" });
    }
});

app.Run();
