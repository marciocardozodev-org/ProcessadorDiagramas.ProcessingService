using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProcessadorDiagramas.ReportingService.Infrastructure.Data;

namespace ProcessadorDiagramas.ReportingService.Tests;

/// <summary>
/// Factory customizada que substitui o DbContext Npgsql por InMemory para testes de integração.
/// Permite rodar testes sem banco de dados real disponível.
/// </summary>
public sealed class ReportingServiceWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContext registrado com Npgsql
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Substitui por InMemory
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("reporting-test-db"));
        });
    }
}
