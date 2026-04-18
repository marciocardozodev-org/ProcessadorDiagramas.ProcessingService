using ProcessadorDiagramas.ProcessingService.Application;
using ProcessadorDiagramas.ProcessingService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
