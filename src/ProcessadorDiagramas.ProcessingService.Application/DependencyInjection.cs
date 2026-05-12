using Microsoft.Extensions.DependencyInjection;
using ProcessadorDiagramas.ProcessingService.Application.Commands.CreateDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Commands.ProcessDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.EventHandlers;
using ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJob;
using ProcessadorDiagramas.ProcessingService.Application.Queries.GetDiagramProcessingJobByAnalysisProcessId;

namespace ProcessadorDiagramas.ProcessingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateDiagramProcessingJobCommandHandler>();
        services.AddScoped<ProcessDiagramProcessingJobCommandHandler>();
        services.AddScoped<GetDiagramProcessingJobQueryHandler>();
        services.AddScoped<GetDiagramProcessingJobByAnalysisProcessIdQueryHandler>();
        services.AddScoped<AnalysisProcessRequestedEventHandler>();
        services.AddScoped<IEventHandler>(serviceProvider =>
            serviceProvider.GetRequiredService<AnalysisProcessRequestedEventHandler>());

        return services;
    }
}