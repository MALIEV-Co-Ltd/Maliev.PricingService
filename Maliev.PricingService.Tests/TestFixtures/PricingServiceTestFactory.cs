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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

    private readonly RSA _testRsa = RSA.Create(2048);

    public string ConnectionString => _dbContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("CORS:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Features:FailOpenOnIAMError", "true");

        // Use actual Testcontainer connection strings for all services
        builder.UseSetting("ConnectionStrings:PricingDbContext", _dbContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", _redisContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:rabbitmq", _rabbitMqContainer.GetConnectionString());
        
        // The service expects Jwt:PublicKey to be a Base64-encoded PEM string
        var publicKeyPem = _testRsa.ExportSubjectPublicKeyInfoPem();
        var publicKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKeyPem));
        builder.UseSetting("Jwt:PublicKey", publicKeyBase64);
        builder.UseSetting("Jwt:Issuer", "test");
        builder.UseSetting("Jwt:Audience", "test");

        builder.ConfigureServices(services =>
        {
            // Remove real DbContext registration
            services.RemoveAll<DbContextOptions<PricingDbContext>>();
            services.RemoveAll<PricingDbContext>();

            // Add Testcontainer DbContext
            services.AddDbContext<PricingDbContext>(options =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
            });

            // Configure JWT validation for tests
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = false,
                    SignatureValidator = delegate (string token, TokenValidationParameters parameters)
                    {
                        return new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
                    },
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });
        });

        // Register RSA key for IAMTestHelpers
        Maliev.Aspire.ServiceDefaults.Testing.IAMTestHelpers.SetTestRSA(_testRsa);
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
        _testRsa.Dispose();
    }
}
