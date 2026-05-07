using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using ProcessadorDiagramas.ProcessingService.API;
using ProcessadorDiagramas.ProcessingService.Application;
using ProcessadorDiagramas.ProcessingService.Infrastructure;

// Lambda mode: AWS_LAMBDA_RUNTIME_API is set by the Lambda execution environment.
if (Environment.GetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API") is not null)
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, configuration) =>
        {
            AwsRuntimeConfigurationBootstrapper.AddAwsRuntimeSecrets(configuration);
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddJsonConsole();
        })
        .ConfigureServices((ctx, services) =>
        {
            services.AddApplication();
            services.AddInfrastructure(ctx.Configuration, ctx.HostingEnvironment);
        })
        .Build();

    await host.StartAsync();

    var function = new LambdaFunction(host.Services);
    await LambdaBootstrapBuilder
        .Create<SQSEvent, SQSBatchResponse>(function.HandleAsync, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();

    await host.StopAsync();
    return;
}

// Web / dev mode: ASP.NET Core with health endpoints and SQS poller (via BackgroundService).
var builder = WebApplication.CreateBuilder(args);

AwsRuntimeConfigurationBootstrapper.AddAwsRuntimeSecrets(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
	service = "ProcessadorDiagramas.ProcessingService",
	role = "processing-worker",
	environment = app.Environment.EnvironmentName
}));

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

app.Run();

public partial class Program;
