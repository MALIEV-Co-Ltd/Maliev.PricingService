using Maliev.PricingService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Application.Services;

public class VolumeDiscountResolver : IVolumeDiscountResolver
{
    private readonly IPricingDbContext _context;

    public VolumeDiscountResolver(IPricingDbContext context)
    {
        _context = context;
    }

    public async Task<(Guid? TierId, decimal DiscountPercent)> ResolveAsync(int quantity, CancellationToken cancellationToken = default)
    {
        var tier = await _context.VolumeDiscountTiers
            .AsNoTracking()
            .Where(t => t.IsActive
                     && t.MinQuantity <= quantity
                     && (t.MaxQuantity == null || t.MaxQuantity >= quantity))
            .OrderByDescending(t => t.DiscountPercent)
            .FirstOrDefaultAsync(cancellationToken);

        return tier is null ? ((Guid?)null, 0m) : (tier.Id, tier.DiscountPercent);
    }
}
