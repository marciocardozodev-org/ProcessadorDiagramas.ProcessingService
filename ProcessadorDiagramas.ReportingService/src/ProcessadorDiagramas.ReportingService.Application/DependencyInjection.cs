using Microsoft.Extensions.DependencyInjection;
using ProcessadorDiagramas.ReportingService.Application.Commands.RegenerateAnalysisReport;
using ProcessadorDiagramas.ReportingService.Application.Queries.GetOrGenerateAnalysisReport;

namespace ProcessadorDiagramas.ReportingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetOrGenerateAnalysisReportQueryHandler>();
        services.AddScoped<RegenerateAnalysisReportCommandHandler>();

        return services;
    }
}
