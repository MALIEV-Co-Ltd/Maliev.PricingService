using Maliev.Aspire.ServiceDefaults;
using Maliev.PricingService.Api.Consumers;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Application;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Infrastructure;
using Maliev.PricingService.Infrastructure.Clients;
using Maliev.PricingService.Infrastructure.Data.SeedData;
using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Log.StartingHost(bootstrapLogger, "Pricing Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults();
    builder.AddStandardMiddleware(options => { options.EnableRequestLogging = true; });
    builder.AddServiceMeters("pricing-meter");

    // Add PostgreSQL DbContext
    builder.AddPostgresDbContext<PricingDbContext>(connectionName: "PricingDbContext");

    // Add Redis Distributed Cache
    builder.AddStandardCache("pricing:");
    builder.Services.AddMemoryCache();

    // Add MassTransit with RabbitMQ
    builder.AddMassTransitWithRabbitMq(x =>
    {
        x.AddEntityFrameworkOutbox<PricingDbContext>(options =>
        {
            _ = options.UsePostgres();
            options.UseBusOutbox();
        });

        // FileAnalyzedEventConsumer disabled — replaced by frontend-triggered flow
        // x.AddConsumer<FileAnalyzedEventConsumer>();
        x.AddConsumer<OrderCompletedEventConsumer>();
    });

    // --- API Configuration ---
    builder.AddStandardCors();
    builder.AddDefaultApiVersioning();
    builder.Services.AddResponseCaching();
    builder.AddJwtAuthentication();
    builder.Services.AddPermissionAuthorization();

    // --- Layer Registration ---
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // External Client Registrations
    builder.AddAuthenticatedServiceClient<IMaterialServiceClient, MaterialServiceClient>(
        "MaterialService",
        sourceServiceName: "pricing");

    builder.Services.AddHttpClient<ICurrencyServiceClient, CurrencyServiceClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ExternalServices:CurrencyService"] ?? "http://currency-service");
    }).AddStandardResilienceHandler();

    // Add OpenAPI
    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Pricing Service API",
            description: "Pricing calculation and audit service.");
    }

    // IAM Registration
    builder.AddIAMServiceClient("pricing");
    builder.Services.AddIAMRegistration<PricingIAMRegistrationService>("pricing");

    // Service-to-service client — uses ServiceAccountAuthenticationHandler for JWT
    builder.AddAuthenticatedServiceClient<IJobServiceClient, JobServiceClient>("JobService");

    // Background workers
    builder.Services.AddHostedService<MaterialDensitySyncWorker>();

    builder.Services.AddControllers();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    await app.MigrateDatabaseAsync<PricingDbContext>();
    await app.SeedLeadTimeOptionsAsync();
    await app.SeedVolumeDiscountTiersAsync();
    await app.SeedMachineCapacityConfigsAsync();
    await app.SeedPricingConfigurationsAsync();

    app.UseStandardMiddleware();
    if (!app.Environment.IsDevelopment()) { app.UseHttpsRedirection(); }
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseResponseCaching();
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "pricing");
    app.MapApiDocumentation(servicePrefix: "pricing");

    Log.ServiceStarted(logger, "Pricing Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.HostTerminated(bootstrapLogger, ex, "Pricing Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// The main program class for the Pricing Service.
/// </summary>
public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);
    }
}
