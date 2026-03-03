using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Application.Interfaces;
using Moq;

namespace Maliev.PricingService.Tests.TestFixtures;

/// <summary>
/// Test factory for integration tests.
/// IMPORTANT: Use Testcontainers for PostgreSQL instead of InMemoryDatabase (banned per AGENTS.md).
/// This file demonstrates the correct pattern but requires Docker to be running.
/// </summary>
public class PricingServiceTestFactory : WebApplicationFactory<Program>
{
    // TODO: Implement Testcontainers-based setup
    // private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
    //     .WithImage("postgres:18-alpine")
    //     .WithDatabase("pricing_test")
    //     .Build();
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PricingDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // VIOLATION FIX REQUIRED: Replace with Testcontainers PostgreSQL
            // Currently using InMemoryDatabase which is banned per AGENTS.md
            // Expected implementation:
            // services.AddDbContext<PricingDbContext>(options =>
            // {
            //     options.UseNpgsql(_postgresContainer.GetConnectionString());
            // });
            
            // Placeholder - needs Testcontainers setup
            services.AddDbContext<PricingDbContext>(options =>
            {
                // TODO: Use Testcontainers PostgreSQL here
                // options.UseNpgsql("...");
            });

            // Mock External Clients
            services.AddScoped<IMaterialServiceClient>(_ => new Mock<IMaterialServiceClient>().Object);
            services.AddScoped<ICurrencyServiceClient>(_ => new Mock<ICurrencyServiceClient>().Object);
        });
    }
}
