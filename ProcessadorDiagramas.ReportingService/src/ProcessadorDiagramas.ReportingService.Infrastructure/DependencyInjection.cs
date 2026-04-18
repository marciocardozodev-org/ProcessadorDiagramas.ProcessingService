using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ReportingService.Application.Interfaces;
using ProcessadorDiagramas.ReportingService.Domain.Interfaces;
using ProcessadorDiagramas.ReportingService.Infrastructure.Clients;
using ProcessadorDiagramas.ReportingService.Infrastructure.Data;
using ProcessadorDiagramas.ReportingService.Infrastructure.Data.Repositories;

namespace ProcessadorDiagramas.ReportingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAnalysisReportRepository, AnalysisReportRepository>();

        services.Configure<ProcessingServiceSettings>(configuration.GetSection("ProcessingService"));
        services.AddHttpClient<ProcessingServiceClient>((serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ProcessingServiceSettings>>().Value;

            if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
                httpClient.BaseAddress = baseUri;

            if (settings.TimeoutSeconds > 0)
                httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });
        services.AddScoped<IProcessingServiceClient>(sp => sp.GetRequiredService<ProcessingServiceClient>());

        return services;
    }
}
