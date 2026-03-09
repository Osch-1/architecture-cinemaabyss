using Confluent.Kafka;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsService.Services;
using EventsService.Models;

var builder = WebApplication.CreateBuilder( args );

// Configuration from environment
var port = Environment.GetEnvironmentVariable( "PORT" ) ?? "8082";
var kafkaBrokers = ( Environment.GetEnvironmentVariable( "KAFKA_BROKERS" ) ?? "kafka:9092" ).Trim();

builder.WebHost.UseUrls( $"http://0.0.0.0:{port}" );

// JSON: snake_case for request binding and responses
builder.Services.ConfigureHttpJsonOptions( opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
} );

// Kafka producer — singleton, thread-safe
builder.Services.AddSingleton<IProducer<string, string>>(
    new ProducerBuilder<string, string>( new ProducerConfig { BootstrapServers = kafkaBrokers } ).Build() );

// Kafka consumer background service
builder.Services.AddHostedService( sp =>
    new KafkaConsumerService(
        sp.GetRequiredService<ILogger<KafkaConsumerService>>(),
        kafkaBrokers ) );

var app = builder.Build();
var logger = app.Logger;

logger.LogInformation( "Events service starting on port {Port}", port );
logger.LogInformation( "Kafka brokers: {KafkaBrokers}", kafkaBrokers );

// JSON options used for manual payload serialization
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
};

// ── Helper ─────────────────────────────────────────────────────────────────────

async Task<IResult> PublishEvent<T>( T input, string topic, string eventType, string eventId,
                                     IProducer<string, string> producer )
{
    var payloadElement = JsonSerializer.SerializeToElement( input, jsonOptions );
    var envelope = new EventEnvelope( eventId, eventType, DateTimeOffset.UtcNow, payloadElement );
    var message = JsonSerializer.Serialize( envelope, jsonOptions );

    var delivery = await producer.ProduceAsync( topic,
        new Message<string, string> { Key = eventId, Value = message } );

    logger.LogInformation(
        "[{Topic}] Produced | partition={Partition} offset={Offset} | key={Key}",
        topic, delivery.Partition.Value, delivery.Offset.Value, eventId );

    return Results.Created( $"/api/events/{eventType}/{eventId}",
        new EventResponse( "success", delivery.Partition.Value, ( int )delivery.Offset.Value, envelope ) );
}

// ── Health ─────────────────────────────────────────────────────────────────────

app.MapGet( "/api/events/health", () => Results.Ok( new { status = true } ) );

// ── Event endpoints ────────────────────────────────────────────────────────────

app.MapPost( "/api/events/movie", async ( MovieEventInput input, IProducer<string, string> producer ) =>
{
    var eventId = $"movie-{input.MovieId}-{input.Action}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    try
    { return await PublishEvent( input, "movie-events", "movie", eventId, producer ); }
    catch ( ProduceException<string, string> ex )
    {
        logger.LogError( ex, "Failed to produce movie event" );
        return Results.Problem( detail: ex.Message, statusCode: 500 );
    }
} );

app.MapPost( "/api/events/user", async ( UserEventInput input, IProducer<string, string> producer ) =>
{
    var eventId = $"user-{input.UserId}-{input.Action}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    try
    { return await PublishEvent( input, "user-events", "user", eventId, producer ); }
    catch ( ProduceException<string, string> ex )
    {
        logger.LogError( ex, "Failed to produce user event" );
        return Results.Problem( detail: ex.Message, statusCode: 500 );
    }
} );

app.MapPost( "/api/events/payment", async ( PaymentEventInput input, IProducer<string, string> producer ) =>
{
    var eventId = $"payment-{input.PaymentId}-{input.Status}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    try
    { return await PublishEvent( input, "payment-events", "payment", eventId, producer ); }
    catch ( ProduceException<string, string> ex )
    {
        logger.LogError( ex, "Failed to produce payment event" );
        return Results.Problem( detail: ex.Message, statusCode: 500 );
    }
} );

app.Run();
