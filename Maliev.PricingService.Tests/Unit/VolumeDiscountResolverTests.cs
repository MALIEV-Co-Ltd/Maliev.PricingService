using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies VolumeDiscountResolver picks the correct tier for a given quantity.
/// Uses SQLite in-memory so EF query behavior stays relational without requiring Postgres.
/// </summary>
public class VolumeDiscountResolverTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PricingDbContext _db;

    public VolumeDiscountResolverTests()
    {
        (_connection, _db) = CreateDbContext();

        _db.VolumeDiscountTiers.AddRange(
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 1,   MaxQuantity = 9,   DiscountPercent = 0m,  IsActive = true, SortOrder = 0 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 10,  MaxQuantity = 24,  DiscountPercent = 5m,  IsActive = true, SortOrder = 1 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 25,  MaxQuantity = 49,  DiscountPercent = 10m, IsActive = true, SortOrder = 2 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 50,  MaxQuantity = 99,  DiscountPercent = 15m, IsActive = true, SortOrder = 3 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 100, MaxQuantity = 499, DiscountPercent = 20m, IsActive = true, SortOrder = 4 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 500, MaxQuantity = null,DiscountPercent = 25m, IsActive = true, SortOrder = 5 }
        );
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(1,   0)]
    [InlineData(9,   0)]
    [InlineData(10,  5)]
    [InlineData(24,  5)]
    [InlineData(25,  10)]
    [InlineData(50,  15)]
    [InlineData(100, 20)]
    [InlineData(500, 25)]
    [InlineData(999, 25)]
    public async Task ResolveAsync_ReturnsCorrectDiscountPercent(int quantity, decimal expectedPercent)
    {
        var resolver = new VolumeDiscountResolver(_db);

        var (_, pct) = await resolver.ResolveAsync(quantity);

        Assert.Equal(expectedPercent, pct);
    }

    [Fact]
    public async Task ResolveAsync_NoMatchingTier_ReturnsZeroDiscount()
    {
        var (emptyConnection, emptyDb) = CreateDbContext();
        using var emptyConnectionScope = emptyConnection;
        using var emptyDbScope = emptyDb;

        var resolver = new VolumeDiscountResolver(emptyDb);

        var (tierId, pct) = await resolver.ResolveAsync(50);

        Assert.Null(tierId);
        Assert.Equal(0m, pct);

    }

    [Fact]
    public async Task ResolveAsync_InactiveTierForQuantity_ReturnsZeroDiscount()
    {
        var (connection, db) = CreateDbContext();
        using var connectionScope = connection;
        using var dbScope = db;

        db.VolumeDiscountTiers.Add(new VolumeDiscountTier
        {
            Id = Guid.NewGuid(), MinQuantity = 10, MaxQuantity = 99,
            DiscountPercent = 20m, IsActive = false, SortOrder = 0
        });
        db.SaveChanges();

        var resolver = new VolumeDiscountResolver(db);
        var (tierId, pct) = await resolver.ResolveAsync(50);

        Assert.Null(tierId);
        Assert.Equal(0m, pct);

    }

    [Fact]
    public async Task ResolveAsync_MatchingTier_ReturnsTierId()
    {
        var tierId = Guid.NewGuid();
        var (connection, db) = CreateDbContext();
        using var connectionScope = connection;
        using var dbScope = db;

        db.VolumeDiscountTiers.Add(new VolumeDiscountTier
        {
            Id = tierId, MinQuantity = 10, MaxQuantity = 99,
            DiscountPercent = 15m, IsActive = true, SortOrder = 0
        });
        db.SaveChanges();

        var resolver = new VolumeDiscountResolver(db);
        var (resolvedId, _) = await resolver.ResolveAsync(50);

        Assert.Equal(tierId, resolvedId);

    }

    private static (SqliteConnection Connection, PricingDbContext DbContext) CreateDbContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var dbContext = new PricingDbContext(
            new DbContextOptionsBuilder<PricingDbContext>()
                .UseSqlite(connection)
                .Options);

        dbContext.Database.EnsureCreated();

        return (connection, dbContext);
    }
}
