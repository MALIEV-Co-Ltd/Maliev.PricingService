using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.RabbitMq;
using System.Security.Cryptography;
using System.Text;

namespace Maliev.PricingService.Tests.TestFixtures;

public class PricingServiceTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("pricing_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public string ConnectionString => _dbContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use actual Testcontainer connection strings for all services
        builder.UseSetting("ConnectionStrings:PricingDbContext", _dbContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", _redisContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:rabbitmq", _rabbitMqContainer.GetConnectionString());
        
        // Dynamically create RSA key for testing
        // The service expects Jwt:PublicKey to be a Base64-encoded PEM string
        using var rsa = RSA.Create(2048);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        var publicKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKeyPem));
        builder.UseSetting("Jwt:PublicKey", publicKeyBase64);

        builder.ConfigureServices(services =>
        {
            // Remove real DbContext registration to ensure we use the one configured here
            // (though setting the ConnectionString above might be enough for AddPostgresDbContext, 
            // explicit replacement ensures we control the options)
            services.RemoveAll<DbContextOptions<PricingDbContext>>();
            services.RemoveAll<PricingDbContext>();

            // Add Testcontainer DbContext
            services.AddDbContext<PricingDbContext>(options =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
            });

            // Add missing authorization policy for tests
            services.AddAuthorization(options =>
            {
                options.AddPolicy(PricingPermissions.CalculationsCreate, policy => 
                    policy.RequireAssertion(_ => true)); // Allow everyone in tests
            });
        });
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _dbContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _rabbitMqContainer.StartAsync()
        );

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.WhenAll(
            _dbContainer.StopAsync(),
            _redisContainer.StopAsync(),
            _rabbitMqContainer.StopAsync()
        );
    }
}
