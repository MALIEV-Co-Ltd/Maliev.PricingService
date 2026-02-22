# Data Model: Deterministic Pricing Engine

**Feature**: 002-deterministic-pricing-engine  
**Date**: 2026-02-22

## New Types

### ManufacturingTechnology Enum

```csharp
namespace Maliev.PricingService.Api.Services;

public enum ManufacturingTechnology
{
    Fdm = 1,
    Sla = 2,
    Cnc = 3,
    Scanning = 4,
    Design = 5
}
```

| Value | Technology | Description |
|-------|------------|-------------|
| Fdm | Fused Deposition Modeling | Layer-by-layer filament extrusion |
| Sla | Stereolithography | UV-cured resin printing |
| Cnc | CNC Machining | Subtractive manufacturing |
| Scanning | 3D Scanning | Digital capture service |
| Design | 3D Design | CAD design service |

---

### MaterialData Record

```csharp
namespace Maliev.PricingService.Api.Services;

public record MaterialData
{
    public decimal Density { get; init; }              // g/cm³
    public decimal CostPerKg { get; init; }            // THB/kg
    
    // FDM parameters
    public decimal FdmVolumetricFlowRate { get; init; }  // mm³/s
    public decimal FdmMinLayerTime { get; init; }        // seconds per layer
    
    // SLA parameters
    public decimal SlaLayerExposure { get; init; }       // seconds per layer
    public decimal SlaLiftTime { get; init; }            // seconds per layer
    
    // CNC parameters
    public decimal CncMachinabilityRating { get; init; } // 0.0–1.0
}
```

**Mapping from MaterialDto**:
```
Density ← MaterialDto.DensityGramPerCm3
CostPerKg ← MaterialDto.CostPerKg
FdmVolumetricFlowRate ← ProcessParameters["FdmVolumetricFlowRate"]
FdmMinLayerTime ← ProcessParameters["FdmMinLayerTime"]
SlaLayerExposure ← ProcessParameters["SlaLayerExposure"]
SlaLiftTime ← ProcessParameters["SlaLiftTime"]
CncMachinabilityRating ← ProcessParameters["CncMachinabilityRating"]
```

---

### MachineRates Record

```csharp
namespace Maliev.PricingService.Api.Services;

public record MachineRates
{
    public decimal FdmMachineHourlyRate { get; init; }  // THB/hour
    public decimal FdmSetupFee { get; init; }           // THB flat
    
    public decimal SlaMachineHourlyRate { get; init; }
    public decimal SlaSetupFee { get; init; }
    
    public decimal CncMachineHourlyRate { get; init; }
    public decimal CncSetupFee { get; init; }
    public decimal CncMaterialRemovalRate { get; init; } // mm³/hour (MRR)
}
```

**Configuration Path**: `Pricing:MachineRates`

**Default Values** (from spec):
```json
{
  "Pricing": {
    "MachineRates": {
      "FdmMachineHourlyRate": 150.0,
      "FdmSetupFee": 50.0,
      "SlaMachineHourlyRate": 200.0,
      "SlaSetupFee": 80.0,
      "CncMachineHourlyRate": 800.0,
      "CncSetupFee": 500.0,
      "CncMaterialRemovalRate": 5000.0
    }
  }
}
```

---

## Modified Types

### PricingRequest Record

**Current Fields** (preserved):
- `FileId`, `CustomerId`, `MaterialId`, `MaterialCode`
- `ManufacturingProcessId`, `ManufacturingProcessName` (now optional)
- `Quantity`, `Geometry`, `CorrelationId`

**New Fields**:

```csharp
/// <summary>
/// The manufacturing technology selected by the customer.
/// </summary>
public required ManufacturingTechnology Technology { get; init; }

/// <summary>
/// Layer height in millimeters (e.g., 0.2 for FDM, 0.05 for SLA).
/// </summary>
public decimal LayerHeightMm { get; init; } = 0.2m;

/// <summary>
/// Whether support structures are enabled. Adds 20% material volume.
/// </summary>
public bool SupportEnabled { get; init; } = false;

/// <summary>
/// Part height along Z-axis in millimeters. Used for layer count calculation.
/// Populated from Geometry.BoundingBoxZ when not explicitly provided.
/// </summary>
public decimal HeightMm { get; init; }

/// <summary>
/// For Scanning technology only. "RawScan" (default) or "ReverseEngineering".
/// Null for non-scanning technologies.
/// </summary>
public string? ScanningTier { get; init; }
```

---

### PricingResult Record

**New Field**:

```csharp
/// <summary>
/// Additional notes about the pricing (e.g., manual confirmation required).
/// </summary>
public string? Notes { get; init; }
```

**Fields to Remove**:
- `MLModelVersion` (no longer applicable)

---

### PricingAuditRecord Entity

**New Fields**:

```csharp
/// <summary>
/// Manufacturing technology used for calculation.
/// </summary>
[StringLength(20)]
public string Technology { get; set; } = string.Empty;
```

**Fields to Remove**:
- `MLModelVersion`
- `ConfigMaterialPricePerCm3`
- `ConfigSupportPricePerCm3`

**Note**: `PricingConfigurationId` remains but becomes nullable since configuration lookup is removed.

---

### PricingStrategy Enum

**Before**:
```csharp
public enum PricingStrategy
{
    RuleBased = 1,
    MLEnhanced = 2,
    Manual = 3,
    Hybrid = 4
}
```

**After**:
```csharp
public enum PricingStrategy
{
    RuleBased = 1,
    Manual = 3
}
```

**Impact**: 
- Remove `MLEnhanced = 2` and `Hybrid = 4`
- `Manual = 3` value preserved for backwards compatibility
- Database enum values must be updated

---

## Entity Relationships

```
┌─────────────────┐
│ PricingRequest  │
├─────────────────┤         ┌──────────────────┐
│ FileId          │         │ GeometryMetrics  │
│ CustomerId      │────────►│ VolumeCm3        │
│ MaterialId      │         │ SurfaceAreaCm2   │
│ Technology ──── │         │ BoundingBoxX/Y/Z │
│ LayerHeightMm   │         └──────────────────┘
│ SupportEnabled  │
│ HeightMm        │
│ ScanningTier    │
│ Quantity        │
└─────────────────┘
         │
         │ resolves
         ▼
┌─────────────────┐         ┌──────────────────┐
│  MaterialData   │         │   MachineRates   │
├─────────────────┤         ├──────────────────┤
│ Density         │         │ FdmMachineRate   │
│ CostPerKg       │         │ FdmSetupFee      │
│ FdmFlowRate     │         │ SlaMachineRate   │
│ SlaExposure     │         │ SlaSetupFee      │
│ CncMachinability│         │ CncMachineRate   │
└─────────────────┘         │ CncSetupFee      │
                            │ CncMRR           │
                            └──────────────────┘
         │                          │
         └──────────┬───────────────┘
                    │
                    │ input to
                    ▼
            ┌───────────────────┐
            │ IPricingCalculator │
            ├───────────────────┤
            │ FdmCalculator     │
            │ SlaCalculator     │
            │ CncCalculator     │
            │ ScanningCalculator│
            │ DesignCalculator  │
            └───────────────────┘
                    │
                    │ produces
                    ▼
            ┌─────────────────┐
            │  PricingResult  │
            ├─────────────────┤
            │ MaterialCost    │
            │ MachineTimeCost │
            │ SetupCost       │
            │ TotalUnitPrice  │
            │ Notes           │
            └─────────────────┘
```

---

## Validation Rules

| Field | Rule |
|-------|------|
| `Technology` | Required, must be valid enum value |
| `LayerHeightMm` | Must be > 0, default 0.2 |
| `HeightMm` | Must be > 0, or derived from `BoundingBoxZ` |
| `ScanningTier` | Only valid when Technology = Scanning; null otherwise |
| `Density` | Must be > 0 |
| `CostPerKg` | Must be > 0 |
| `FdmVolumetricFlowRate` | Must be > 0 |
| `CncMachinabilityRating` | Must be 0.0–1.0 |

---

## Migration Notes

### Data Entity Changes

The `PricingAuditRecord` entity changes require:
1. EF Core migration to add `Technology` column
2. EF Core migration to remove `MLModelVersion`, `ConfigMaterialPricePerCm3`, `ConfigSupportPricePerCm3` columns
3. Update existing rows with default Technology value (likely "Fdm" for historical records)

### Enum Value Cleanup

The `PricingStrategy` enum change in both:
- `Maliev.PricingService.Api/Services/PricingModels.cs`
- `Maliev.PricingService.Data/Entities/PricingAuditRecord.cs`

Must be synchronized to avoid serialization issues.
