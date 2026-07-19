namespace Maliev.PricingService.Application.Interfaces;

public interface IVolumeDiscountResolver
{
    Task<(Guid? TierId, decimal DiscountPercent)> ResolveAsync(int quantity, CancellationToken cancellationToken = default);
}
