namespace EventsService.Models;

internal record MovieEventInput(
    int MovieId,
    string Title,
    string Action,
    int? UserId = null,
    float? Rating = null,
    string[]? Genres = null,
    string? Description = null);

internal record UserEventInput(
    int UserId,
    string Action,
    DateTimeOffset Timestamp,
    string? Username = null,
    string? Email = null);

internal record PaymentEventInput(
    int PaymentId,
    int UserId,
    float Amount,
    string Status,
    DateTimeOffset Timestamp,
    string? MethodType = null);
