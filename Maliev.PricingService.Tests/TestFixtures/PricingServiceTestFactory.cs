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

public class PricingServiceTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PricingDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add InMemory Database
            services.AddDbContext<PricingDbContext>(options =>
            {
                options.UseInMemoryDatabase("PricingTestDb");
            });

            // Mock External Clients
            services.AddScoped<IMaterialServiceClient>(_ => new Mock<IMaterialServiceClient>().Object);
            services.AddScoped<ICurrencyServiceClient>(_ => new Mock<ICurrencyServiceClient>().Object);
        });
    }
}
