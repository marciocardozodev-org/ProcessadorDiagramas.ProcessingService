using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.ProcessingService.Application.Interfaces;
using ProcessadorDiagramas.ProcessingService.Domain.Interfaces;
using ProcessadorDiagramas.ProcessingService.Infrastructure.BackgroundServices;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Data;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Data.Repositories;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Processing;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Storage;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure;

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

        services.AddScoped<IDiagramProcessingJobRepository, DiagramProcessingJobRepository>();
        services.AddScoped<IDiagramProcessingResultRepository, DiagramProcessingResultRepository>();
        services.AddScoped<IDiagramProcessingAttemptRepository, DiagramProcessingAttemptRepository>();
        services.AddScoped<IDiagramSourceStorage, LocalDiagramSourceStorage>();
        services.AddScoped<IDiagramPreprocessor, DefaultDiagramPreprocessor>();

        services.Configure<AiProviderSettings>(configuration.GetSection("AiProvider"));
        services.AddHttpClient<OpenAiCompatibleDiagramAiPipeline>((serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<AiProviderSettings>>().Value;

            if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
                httpClient.BaseAddress = baseUri;

            if (settings.TimeoutSeconds > 0)
                httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        services.AddScoped<DummyDiagramAiPipeline>();
        services.AddScoped<IDiagramAiPipeline>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<AiProviderSettings>>().Value;

            return settings.Enabled
                && settings.Provider.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<OpenAiCompatibleDiagramAiPipeline>()
                : serviceProvider.GetRequiredService<DummyDiagramAiPipeline>();
        });

        var enableAwsServices = configuration.GetValue<bool>("EnableAwsServices");

        if (enableAwsServices)
        {
            var awsSection = configuration.GetSection("Aws");
            services.Configure<AwsSettings>(awsSection);

            var awsSettings = awsSection.Get<AwsSettings>() ?? new AwsSettings();
            services.AddSingleton<IAmazonSimpleNotificationService>(_ => CreateSnsClient(awsSettings));
            services.AddSingleton<IAmazonSQS>(_ => CreateSqsClient(awsSettings));
            services.AddScoped<IMessageBus, AwsMessageBus>();
            services.AddHostedService<ProcessingInboxConsumer>();
        }
        else
        {
            services.AddScoped<IMessageBus, DummyMessageBus>();
        }

        return services;
    }

    private static IAmazonSimpleNotificationService CreateSnsClient(AwsSettings settings)
    {
        var credentials = CreateCredentials();

        if (!string.IsNullOrWhiteSpace(settings.ServiceURL))
        {
            return new AmazonSimpleNotificationServiceClient(credentials, new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = settings.ServiceURL,
                AuthenticationRegion = settings.Region,
                UseHttp = settings.ServiceURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            });
        }

        return new AmazonSimpleNotificationServiceClient(credentials, Amazon.RegionEndpoint.GetBySystemName(settings.Region));
    }

    private static IAmazonSQS CreateSqsClient(AwsSettings settings)
    {
        var credentials = CreateCredentials();

        if (!string.IsNullOrWhiteSpace(settings.ServiceURL))
        {
            return new AmazonSQSClient(credentials, new AmazonSQSConfig
            {
                ServiceURL = settings.ServiceURL,
                AuthenticationRegion = settings.Region,
                UseHttp = settings.ServiceURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            });
        }

        return new AmazonSQSClient(credentials, Amazon.RegionEndpoint.GetBySystemName(settings.Region));
    }

    private static BasicAWSCredentials CreateCredentials()
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";

        return new BasicAWSCredentials(accessKey, secretKey);
    }
}