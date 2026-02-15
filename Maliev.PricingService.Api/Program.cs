using Maliev.Aspire.ServiceDefaults;
using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Api.Consumers;
using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Log.StartingHost(bootstrapLogger, "Pricing Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });
    builder.AddServiceMeters("pricing-meter"); // Register service meters for OpenTelemetry business metrics

    // Add PostgreSQL DbContext
    builder.AddPostgresDbContext<PricingDbContext>(connectionName: "PricingDbContext");

    // Add Redis Distributed Cache
    builder.AddStandardCache("pricing:"); // Redis + in-memory fallback, memory-optimized

    // Add in-memory cache for pricing configurations
    builder.Services.AddMemoryCache();

    // Add MassTransit with RabbitMQ
    builder.AddMassTransitWithRabbitMq(x =>
    {
        x.AddConsumer<FileAnalyzedEventConsumer>();
        x.AddConsumer<OrderCompletedEventConsumer>();
    });

    // --- API Configuration ---
    builder.AddStandardCors(); // CORS with fail-fast validation
    builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

    // JWT Authentication
    builder.AddJwtAuthentication();

    // Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Pricing Service API",
            description: "Pricing calculation and audit service. Provides instant price calculations using rule-based and ML algorithms, maintains complete audit trail for all pricing decisions, and integrates with MaterialService and CurrencyService for accurate pricing.");
    }

    // --- External Service Clients ---
    builder.AddServiceClient<IMaterialServiceClient, MaterialServiceClient>("MaterialService");
    builder.AddServiceClient<ICurrencyServiceClient, CurrencyServiceClient>("CurrencyService");

    // IAM Registration
    builder.AddIAMServiceClient("pricing");
    builder.Services.AddIAMRegistration<PricingIAMRegistrationService>("pricing");

    // --- Application Services ---
    builder.Services.AddScoped<IPricingEngine, RuleBasedPricingEngine>();
    builder.Services.AddScoped<IMLPricingEngine, MLPricingEngine>();
    builder.Services.AddScoped<IPricingOrchestrator, PricingOrchestrator>();

    // Authorization
    builder.Services.AddPermissionAuthorization();

    builder.Services.AddControllers();

    var app = builder.Build();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    // Run database migrations on startup
    await app.MigrateDatabaseAsync<PricingDbContext>();

    // Configure middleware pipeline
    app.UseStandardMiddleware();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Map endpoints after middleware
    app.MapControllers();

    // Map Aspire default endpoints (/health, /alive, /metrics)
    app.MapDefaultEndpoints(servicePrefix: "pricing");

    // Map OpenAPI and Scalar documentation (dev/staging only)
    app.MapApiDocumentation(servicePrefix: "pricing");

    Log.ServiceStarted(logger, "Pricing Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.HostTerminated(bootstrapLogger, ex, "Pricing Service");
    // Force flush to ensure Aspire captures the error before process exits
    Console.Out.Flush();
    Console.Error.Flush();
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main entry point for the Maliev Pricing Service API.
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

        [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed - application may not function correctly")]
        public static partial void MigrationFailed(ILogger logger, Exception exception);
    }
}
