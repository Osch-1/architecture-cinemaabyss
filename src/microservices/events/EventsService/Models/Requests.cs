namespace EventsService.Models;

record MovieEventInput(
    int       MovieId,
    string    Title,
    string    Action,
    int?      UserId      = null,
    float?    Rating      = null,
    string[]? Genres      = null,
    string?   Description = null );

record UserEventInput(
    int            UserId,
    string         Action,
    DateTimeOffset Timestamp,
    string?        Username = null,
    string?        Email    = null );

record PaymentEventInput(
    int            PaymentId,
    int            UserId,
    float          Amount,
    string         Status,
    DateTimeOffset Timestamp,
    string?        MethodType = null );
