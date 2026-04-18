namespace ProcessadorDiagramas.ReportingService.Infrastructure.Clients;

public sealed class ProcessingServiceSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
