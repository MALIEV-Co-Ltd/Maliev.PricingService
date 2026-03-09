using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Moq;
using Testcontainers.PostgreSql;

namespace Maliev.PricingService.Tests.TestFixtures;

public class PricingServiceTestFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_test")
        .Build();

    public string ConnectionString => _postgresContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PricingDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<PricingDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            services.AddScoped<IMaterialServiceClient>(_ => new Mock<IMaterialServiceClient>().Object);
            services.AddScoped<ICurrencyServiceClient>(_ => new Mock<ICurrencyServiceClient>().Object);
            services.AddScoped<IPricingOrchestrator, PricingOrchestrator>();
        });
    }
}
