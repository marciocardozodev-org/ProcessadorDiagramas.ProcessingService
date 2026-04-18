using System.Text.Json;
using ProcessadorDiagramas.ReportingService.Application.Contracts;

namespace ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;

/// <summary>
/// Responsável por transformar o resultado bruto de IA em campos estruturados do relatório.
/// Extrai seções do JSON do RawAiOutput produzido pelo ProcessingService.
/// </summary>
internal static class AnalysisReportComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Tenta extrair as seções estruturadas do output bruto de IA.
    /// Se o output for JSON com as chaves esperadas, usa-as diretamente.
    /// Caso contrário, encapsula o conteúdo bruto em um formato padrão.
    /// </summary>
    public static (string Components, string Risks, string Recommendations) Compose(
        ProcessingJobResult jobResult)
    {
        var raw = jobResult.RawAiOutput ?? string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var components = TryGetSection(root, "components", "componentes", "identified_components");
            var risks = TryGetSection(root, "risks", "riscos", "architectural_risks", "architecturalRisks");
            var recommendations = TryGetSection(root, "recommendations", "recomendacoes", "recomendações");

            return (
                components ?? WrapRaw("components", raw),
                risks ?? WrapRaw("risks", raw),
                recommendations ?? WrapRaw("recommendations", raw)
            );
        }
        catch (JsonException)
        {
            // Output não é JSON — encapsula o texto bruto
            var wrapped = WrapRaw("raw_output", raw);
            return (wrapped, wrapped, wrapped);
        }
    }

    private static string? TryGetSection(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var prop))
                return prop.ValueKind == JsonValueKind.String
                    ? prop.GetString()
                    : prop.GetRawText();
        }
        return null;
    }

    private static string WrapRaw(string key, string raw)
        => JsonSerializer.Serialize(new Dictionary<string, string> { [key] = raw });
}
