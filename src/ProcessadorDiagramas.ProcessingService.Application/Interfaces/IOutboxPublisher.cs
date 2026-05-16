namespace ProcessadorDiagramas.ProcessingService.Application.Interfaces;

public interface IOutboxPublisher
{
    Task PublishPendingAsync(CancellationToken cancellationToken = default);
}
