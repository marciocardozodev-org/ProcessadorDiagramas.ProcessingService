using Amazon.Runtime;
using Amazon.S3;
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
        services.AddScoped<IDiagramPreprocessor, DefaultDiagramPreprocessor>();

        var storageSection = configuration.GetSection("DiagramSourceStorage");
        services.Configure<DiagramSourceStorageSettings>(storageSection);

        var storageSettings = storageSection.Get<DiagramSourceStorageSettings>() ?? new DiagramSourceStorageSettings();
        var awsSettings = configuration.GetSection("Aws").Get<AwsSettings>() ?? new AwsSettings();

        if (storageSettings.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAmazonS3>(_ => CreateS3Client(awsSettings));
            services.AddScoped<IDiagramSourceStorage, S3DiagramSourceStorage>();
        }
        else
        {
            services.AddScoped<IDiagramSourceStorage, LocalDiagramSourceStorage>();
        }

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

            awsSettings = awsSection.Get<AwsSettings>() ?? new AwsSettings();
            services.AddSingleton<IAmazonSimpleNotificationService>(_ => CreateSnsClient(awsSettings));
            services.AddSingleton<IAmazonSQS>(_ => CreateSqsClient(awsSettings));

            if (!string.IsNullOrWhiteSpace(awsSettings.ServiceURL))
                services.AddScoped<IMessageBus, LocalStackMessageBus>();
            else
                services.AddScoped<IMessageBus, AwsMessageBus>();

            var enableSqsPolling = configuration.GetValue<bool>("Aws:EnableSqsPolling");
            if (enableSqsPolling)
                services.AddHostedService<ProcessingInboxConsumer>();
        }
        else
        {
            services.AddScoped<IMessageBus, DummyMessageBus>();
        }

        services.AddScoped<MessageDispatcher>();

        return services;
    }

    private static IAmazonSimpleNotificationService CreateSnsClient(AwsSettings settings)
    {
        var credentials = CreateCredentials(settings.ServiceURL);

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
        var credentials = CreateCredentials(settings.ServiceURL);

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

    private static IAmazonS3 CreateS3Client(AwsSettings settings)
    {
        var credentials = CreateCredentials(settings.ServiceURL);

        if (!string.IsNullOrWhiteSpace(settings.ServiceURL))
        {
            return new AmazonS3Client(credentials, new AmazonS3Config
            {
                ServiceURL = settings.ServiceURL,
                AuthenticationRegion = settings.Region,
                ForcePathStyle = true,
                UseHttp = settings.ServiceURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            });
        }

        return new AmazonS3Client(credentials, Amazon.RegionEndpoint.GetBySystemName(settings.Region));
    }

    private static AWSCredentials CreateCredentials(string? serviceUrl = null)
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
                     ?? Environment.GetEnvironmentVariable("Aws__AccessKeyId")
                     ?? string.Empty;
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
                     ?? Environment.GetEnvironmentVariable("Aws__SecretAccessKey")
                     ?? string.Empty;
        var sessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN")
                        ?? Environment.GetEnvironmentVariable("Aws__SessionToken");

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            return new BasicAWSCredentials(
                string.IsNullOrWhiteSpace(accessKey) ? "test" : accessKey,
                string.IsNullOrWhiteSpace(secretKey) ? "test" : secretKey);
        }

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            return FallbackCredentialsFactory.GetCredentials();

        if (!string.IsNullOrWhiteSpace(sessionToken))
            return new SessionAWSCredentials(accessKey, secretKey, sessionToken);

        return new BasicAWSCredentials(accessKey, secretKey);
    }
}