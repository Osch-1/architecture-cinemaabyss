using System.Text.Json;

namespace EventsService.Models;

internal record EventEnvelope(string Id, string Type, DateTimeOffset Timestamp, JsonElement Payload);

internal record EventResponse(string Status, int Partition, int Offset, EventEnvelope Event);
