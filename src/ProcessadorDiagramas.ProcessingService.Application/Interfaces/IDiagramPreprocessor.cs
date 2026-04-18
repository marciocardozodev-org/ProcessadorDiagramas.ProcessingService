namespace ProcessadorDiagramas.ProcessingService.Application.Interfaces;

public interface IDiagramPreprocessor
{
    Task<string> PreprocessAsync(StoredDiagramSource source, CancellationToken cancellationToken = default);
}