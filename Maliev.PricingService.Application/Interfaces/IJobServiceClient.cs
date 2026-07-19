namespace Maliev.PricingService.Application.Interfaces;

/// <summary>
/// Client interface for the Job Service to fetch queue depth information.
/// </summary>
public interface IJobServiceClient
{
    /// <summary>
    /// Gets the queue depth (number of active jobs) by technology.
    /// </summary>
    /// <param name="technology">The manufacturing technology (e.g., FDM, SLA, CNC).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A dictionary with technology as key and count of active jobs as value. Returns empty dictionary on failure.</returns>
    Task<Dictionary<string, int>> GetQueueDepthByTechnologyAsync(string? technology, CancellationToken cancellationToken = default);
}
