using System.Text;
using System.Text.Json;
using FluentAssertions;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

namespace ProcessadorDiagramas.ProcessingService.Tests.Infrastructure.Processing;

public sealed class LocalPipelineComponentsTests
{
    [Fact]
    public async Task LocalDiagramSourceStorage_ReadAsync_ReturnsStoredSource()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "diagram-content");

        try
        {
            var storage = new LocalDiagramSourceStorage();

            var source = await storage.ReadAsync(tempFile);

            source.StorageKey.Should().Be(tempFile);
            source.FileName.Should().Be(Path.GetFileName(tempFile));
            Encoding.UTF8.GetString(source.Content).Should().Be("diagram-content");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task DefaultDiagramPreprocessor_PreprocessAsync_TextFile_ReturnsStructuredPayload()
    {
        var preprocessor = new DefaultDiagramPreprocessor();
        var source = new StoredDiagramSource("/tmp/diagram.mmd", "diagram.mmd", "text/plain", Encoding.UTF8.GetBytes("graph TD; A-->B;"));

        var payload = await preprocessor.PreprocessAsync(source);
        using var document = JsonDocument.Parse(payload);

        payload.Should().Contain("diagram.mmd");
        payload.Should().Contain("text");
        document.RootElement.GetProperty("Content").GetString().Should().Be("graph TD; A-->B;");
    }

    [Fact]
    public async Task DefaultDiagramPreprocessor_PreprocessAsync_PdfFile_ReturnsDocumentEnvelope()
    {
        var preprocessor = new DefaultDiagramPreprocessor();
        var source = new StoredDiagramSource("/tmp/diagram.pdf", "diagram.pdf", "application/pdf", [1, 2, 3, 4]);

        var payload = await preprocessor.PreprocessAsync(source);
        using var document = JsonDocument.Parse(payload);

        document.RootElement.GetProperty("InputKind").GetString().Should().Be("document");
        document.RootElement.GetProperty("DetectedFormat").GetString().Should().Be("pdf");
        document.RootElement.GetProperty("ContentEncoding").GetString().Should().Be("base64");
    }

    [Fact]
    public async Task DummyDiagramAiPipeline_AnalyzeAsync_ReturnsRawJson()
    {
        var pipeline = new DummyDiagramAiPipeline();

        var result = await pipeline.AnalyzeAsync("preprocessed-content");

        result.RawOutput.Should().Contain("dummy-ai-pipeline");
        result.RawOutput.Should().Contain("ContentHash");
    }
}