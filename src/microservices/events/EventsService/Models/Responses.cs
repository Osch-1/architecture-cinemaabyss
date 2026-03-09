using System.Text.Json;

namespace EventsService.Models;

record EventEnvelope( string Id, string Type, DateTimeOffset Timestamp, JsonElement Payload );

record EventResponse( string Status, int Partition, int Offset, EventEnvelope Event );
