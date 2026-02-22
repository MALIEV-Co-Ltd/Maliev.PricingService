using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// Client for communicating with MaterialService.
/// </summary>
public interface IMaterialServiceClient
{
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
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>Material code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>Material name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Density in g/cm³.</summary>
    [JsonPropertyName("densityGramPerCm3")]
    public decimal DensityGramPerCm3 { get; init; }

    /// <summary>Cost per kg.</summary>
    [JsonPropertyName("costPerKg")]
    public decimal CostPerKg { get; init; }

    /// <summary>Technology-specific process parameters.</summary>
    [JsonPropertyName("processParameters")]
    public Dictionary<string, string> ProcessParameters { get; init; } = [];
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
