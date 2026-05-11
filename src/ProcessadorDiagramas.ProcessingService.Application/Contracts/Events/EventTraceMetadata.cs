using System.Text.Json.Serialization;

namespace ProcessadorDiagramas.ProcessingService.Application.Contracts.Events;

public sealed record EventTraceMetadata(
    [property: JsonPropertyName("producerService")] string ProducerService,
    [property: JsonPropertyName("producerVersion")] string ProducerVersion,
    [property: JsonPropertyName("messageId")] string MessageId);
