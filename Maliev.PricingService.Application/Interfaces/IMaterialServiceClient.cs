namespace Maliev.PricingService.Application.Interfaces;

/// <summary>
/// Client for communicating with MaterialService.
/// </summary>
public interface IMaterialServiceClient
{
    /// <summary>
    /// Gets the material catalog.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The material catalog.</returns>
    Task<IReadOnlyList<MaterialDto>> GetMaterialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a material by ID.
    /// </summary>
    /// <param name="materialId">The material ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The material details.</returns>
    Task<MaterialDto?> GetMaterialAsync(Guid materialId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a manufacturing process by ID.
    /// </summary>
    /// <param name="processId">The process ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process details.</returns>
    Task<ManufacturingProcessDto?> GetProcessAsync(Guid processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default material for a process type.
    /// </summary>
    /// <param name="processType">The process type (e.g., "FDM", "SLA", "CNC").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default material.</returns>
    Task<MaterialDto?> GetDefaultMaterialAsync(string processType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Material data transfer object.
/// </summary>
public record MaterialDto
{
    /// <summary>Material ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Material code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Material name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Density in g/cm³.</summary>
    public decimal DensityGramPerCm3 { get; init; }

    /// <summary>Price per kg.</summary>
    public decimal PricePerKg { get; init; }
}

/// <summary>
/// Manufacturing process data transfer object.
/// </summary>
public record ManufacturingProcessDto
{
    /// <summary>Process ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Process name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Process type (FDM, SLA, SLS, CNC, etc.).</summary>
    public string Type { get; init; } = string.Empty;
}
