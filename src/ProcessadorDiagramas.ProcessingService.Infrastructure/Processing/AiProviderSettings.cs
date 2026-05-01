namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;

public sealed class AiProviderSettings
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Dummy";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public string PromptTemplate { get; set; } = "Você é um assistente técnico especializado em diagramas de arquitetura e fluxo. Resuma o diagrama, identifique componentes, dependências e riscos principais em português claro.";

    public double Temperature { get; set; } = 0.2;

    public int MaxTokens { get; set; } = 1200;

    public int MaxInputCharacters { get; set; } = 12000;

    public int TimeoutSeconds { get; set; } = 60;
}
