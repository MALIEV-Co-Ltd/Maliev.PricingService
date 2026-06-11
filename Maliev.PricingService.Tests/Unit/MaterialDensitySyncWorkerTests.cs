using System.Reflection;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Maliev.PricingService.Tests.Unit;

public sealed class MaterialDensitySyncWorkerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_density_sync_tests")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task SyncDensitiesAsync_UsesBulkMaterialCatalogInsteadOfPerMaterialRequests()
    {
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;
        await using var db = new PricingDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var firstMaterialId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondMaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        db.Configurations.AddRange(
            CreateConfiguration(firstMaterialId, 1m),
            CreateConfiguration(firstMaterialId, 1m),
            CreateConfiguration(secondMaterialId, 1m));
        await db.SaveChangesAsync();

        var materialClient = new CountingMaterialClient(
            [
                new MaterialDto
                {
                    Id = firstMaterialId,
                    DensityGramPerCm3 = 1.25m,
                },
                new MaterialDto
                {
                    Id = secondMaterialId,
                    DensityGramPerCm3 = 2.5m,
                },
            ]);
        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IMaterialServiceClient>(materialClient)
            .BuildServiceProvider();
        var worker = new MaterialDensitySyncWorker(
            services,
            NullLogger<MaterialDensitySyncWorker>.Instance);

        await InvokeSyncDensitiesAsync(worker);

        Assert.Equal(1, materialClient.GetMaterialsCallCount);
        Assert.Equal(0, materialClient.GetMaterialCallCount);
        Assert.All(db.Configurations.Where(config => config.MaterialId == firstMaterialId), config =>
        {
            Assert.Equal(1.25m, config.DensityGramPerCm3);
        });
        Assert.Equal(2.5m, db.Configurations.Single(config => config.MaterialId == secondMaterialId).DensityGramPerCm3);
    }

    private static async Task InvokeSyncDensitiesAsync(MaterialDensitySyncWorker worker)
    {
        var method = typeof(MaterialDensitySyncWorker).GetMethod(
            "SyncDensitiesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var task = method?.Invoke(worker, [CancellationToken.None]) as Task;
        Assert.NotNull(task);
        await task;
    }

    private static PricingConfiguration CreateConfiguration(Guid materialId, decimal density) => new()
    {
        Id = Guid.NewGuid(),
        MaterialId = materialId,
        ManufacturingProcessId = Guid.NewGuid(),
        MaterialPricePerCm3 = 1m,
        SupportMaterialPricePerCm3 = 0m,
        MachineHourlyRate = 100m,
        PrintSpeedCm3PerHour = 10m,
        DensityGramPerCm3 = density,
        SetupCostFlat = 25m,
        MinimumOrderPrice = 10m,
        MarginMultiplier = 1.5m,
        EffectiveFrom = DateTime.UtcNow.AddDays(-1),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "test",
    };

    private sealed class CountingMaterialClient : IMaterialServiceClient
    {
        private readonly IReadOnlyList<MaterialDto> _materials;

        public CountingMaterialClient(IReadOnlyList<MaterialDto> materials)
        {
            _materials = materials;
        }

        public int GetMaterialsCallCount { get; private set; }

        public int GetMaterialCallCount { get; private set; }

        public Task<IReadOnlyList<MaterialDto>> GetMaterialsAsync(CancellationToken cancellationToken = default)
        {
            GetMaterialsCallCount++;
            return Task.FromResult(_materials);
        }

        public Task<MaterialDto?> GetMaterialAsync(Guid materialId, CancellationToken cancellationToken = default)
        {
            GetMaterialCallCount++;
            return Task.FromResult<MaterialDto?>(null);
        }

        public Task<ManufacturingProcessDto?> GetProcessAsync(Guid processId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManufacturingProcessDto?>(null);
        }

        public Task<MaterialDto?> GetDefaultMaterialAsync(string processType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MaterialDto?>(null);
        }
    }
}
