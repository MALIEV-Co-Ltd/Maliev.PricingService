using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Persistence;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies VolumeDiscountResolver picks the correct tier for a given quantity.
/// Uses PostgreSQL so provider-specific query and concurrency behavior matches production.
/// </summary>
[Collection(PricingConfigurationDatabaseCollection.Name)]
public class VolumeDiscountResolverTests
{
    private readonly PricingConfigurationDatabaseFixture _fixture;

    public VolumeDiscountResolverTests(PricingConfigurationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 5)]
    [InlineData(24, 5)]
    [InlineData(25, 10)]
    [InlineData(50, 15)]
    [InlineData(100, 20)]
    [InlineData(500, 25)]
    [InlineData(999, 25)]
    public async Task ResolveAsync_ReturnsCorrectDiscountPercent(int quantity, decimal expectedPercent)
    {
        await using var db = await CreateSeededDbContextAsync();
        var resolver = new VolumeDiscountResolver(db);

        var (_, pct) = await resolver.ResolveAsync(quantity);

        Assert.Equal(expectedPercent, pct);
    }

    [Fact]
    public async Task ResolveAsync_NoMatchingTier_ReturnsZeroDiscount()
    {
        await using var emptyDb = await _fixture.CreateCleanDbContextAsync();

        var resolver = new VolumeDiscountResolver(emptyDb);

        var (tierId, pct) = await resolver.ResolveAsync(50);

        Assert.Null(tierId);
        Assert.Equal(0m, pct);

    }

    [Fact]
    public async Task ResolveAsync_InactiveTierForQuantity_ReturnsZeroDiscount()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();

        db.VolumeDiscountTiers.Add(new VolumeDiscountTier
        {
            Id = Guid.NewGuid(),
            MinQuantity = 10,
            MaxQuantity = 99,
            DiscountPercent = 20m,
            IsActive = false,
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        var resolver = new VolumeDiscountResolver(db);
        var (tierId, pct) = await resolver.ResolveAsync(50);

        Assert.Null(tierId);
        Assert.Equal(0m, pct);

    }

    [Fact]
    public async Task ResolveAsync_MatchingTier_ReturnsTierId()
    {
        var tierId = Guid.NewGuid();
        await using var db = await _fixture.CreateCleanDbContextAsync();

        db.VolumeDiscountTiers.Add(new VolumeDiscountTier
        {
            Id = tierId,
            MinQuantity = 10,
            MaxQuantity = 99,
            DiscountPercent = 15m,
            IsActive = true,
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        var resolver = new VolumeDiscountResolver(db);
        var (resolvedId, _) = await resolver.ResolveAsync(50);

        Assert.Equal(tierId, resolvedId);

    }

    private async Task<PricingDbContext> CreateSeededDbContextAsync()
    {
        var db = await _fixture.CreateCleanDbContextAsync();
        db.VolumeDiscountTiers.AddRange(
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 1, MaxQuantity = 9, DiscountPercent = 0m, IsActive = true, SortOrder = 0 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 10, MaxQuantity = 24, DiscountPercent = 5m, IsActive = true, SortOrder = 1 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 25, MaxQuantity = 49, DiscountPercent = 10m, IsActive = true, SortOrder = 2 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 50, MaxQuantity = 99, DiscountPercent = 15m, IsActive = true, SortOrder = 3 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 100, MaxQuantity = 499, DiscountPercent = 20m, IsActive = true, SortOrder = 4 },
            new VolumeDiscountTier { Id = Guid.NewGuid(), MinQuantity = 500, MaxQuantity = null, DiscountPercent = 25m, IsActive = true, SortOrder = 5 });
        await db.SaveChangesAsync();
        return db;
    }
}
