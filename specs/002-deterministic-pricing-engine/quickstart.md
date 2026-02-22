# Quickstart: Deterministic Pricing Engine

**Feature**: 002-deterministic-pricing-engine  
**Date**: 2026-02-22

## Prerequisites

1. **MaterialService Dependency**: Verify MaterialService API has these fields:
   - `Density` (decimal, g/cm³)
   - `CostPerKg` (decimal, THB/kg)
   - `ProcessParameters` (Dictionary<string, string>)

2. **Development Environment**:
   - .NET 10 SDK
   - Docker (for integration tests)
   - PostgreSQL (local or container)

---

## Quick Implementation Guide

### Step 1: Delete ML Engine (5 minutes)

```bash
# Delete ML engine files
rm Maliev.PricingService.Api/Services/IMLPricingEngine.cs
rm Maliev.PricingService.Api/Services/MLPricingEngine.cs

# Remove ML registration from Program.cs
# Line to remove: builder.Services.AddScoped<IMLPricingEngine, MLPricingEngine>();
```

### Step 2: Update PricingModels.cs (15 minutes)

```csharp
// Add enum
public enum ManufacturingTechnology
{
    Fdm = 1,
    Sla = 2,
    Cnc = 3,
    Scanning = 4,
    Design = 5
}

// Add to PricingRequest
public required ManufacturingTechnology Technology { get; init; }
public decimal LayerHeightMm { get; init; } = 0.2m;
public bool SupportEnabled { get; init; } = false;
public decimal HeightMm { get; init; }
public string? ScanningTier { get; init; }

// Add new records
public record MaterialData { /* ... */ }
public record MachineRates { /* ... */ }

// Update PricingStrategy enum - remove MLEnhanced and Hybrid
```

### Step 3: Create Calculators (30 minutes)

Create directory and interface:

```bash
mkdir -p Maliev.PricingService.Api/Services/Calculators
```

Create each calculator following the formulas from spec:
- `FdmPricingCalculator.cs` - minimum 300 THB
- `SlaPricingCalculator.cs` - minimum 500 THB
- `CncPricingCalculator.cs` - minimum 2500 THB
- `ScanningPricingCalculator.cs` - 2500/4500 THB tiers
- `DesignPricingCalculator.cs` - 500 THB minimum

### Step 4: Update DI Registration (5 minutes)

In `Program.cs`:

```csharp
// Register MachineRates configuration
builder.Services.Configure<MachineRates>(
    builder.Configuration.GetSection("Pricing:MachineRates"));

// Register calculators
builder.Services.AddTransient<IPricingCalculator, FdmPricingCalculator>();
builder.Services.AddTransient<IPricingCalculator, SlaPricingCalculator>();
builder.Services.AddTransient<IPricingCalculator, CncPricingCalculator>();
builder.Services.AddTransient<IPricingCalculator, ScanningPricingCalculator>();
builder.Services.AddTransient<IPricingCalculator, DesignPricingCalculator>();
```

### Step 5: Add Configuration (2 minutes)

In `appsettings.json`:

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

### Step 6: Build and Test (5 minutes)

```bash
dotnet build
dotnet test
```

---

## Example Usage

### Calculate FDM Price

```http
POST /api/v1/pricing/calculate
Content-Type: application/json

{
  "fileId": "00000000-0000-0000-0000-000000000001",
  "customerId": "00000000-0000-0000-0000-000000000001",
  "materialId": "00000000-0000-0000-0000-000000000001",
  "technology": "Fdm",
  "layerHeightMm": 0.2,
  "supportEnabled": false,
  "heightMm": 10.0,
  "quantity": 1,
  "geometry": {
    "volumeCm3": 1.0,
    "supportVolumeCm3": 0.0,
    "surfaceAreaCm2": 6.0,
    "boundingBoxX": 10.0,
    "boundingBoxY": 10.0,
    "boundingBoxZ": 10.0,
    "isManifold": true,
    "triangleCount": 12
  }
}
```

### Calculate Scanning Price

```http
POST /api/v1/pricing/calculate
Content-Type: application/json

{
  "fileId": "00000000-0000-0000-0000-000000000001",
  "customerId": "00000000-0000-0000-0000-000000000001",
  "materialId": "00000000-0000-0000-0000-000000000001",
  "technology": "Scanning",
  "scanningTier": "ReverseEngineering",
  "quantity": 1,
  "geometry": { ... }
}
```

---

## Verification Checklist

- [ ] `dotnet build` succeeds with zero errors
- [ ] `dotnet test` passes all tests
- [ ] No ML.net references in `.csproj`
- [ ] No `Microsoft.ML` in codebase (`grep -r "Microsoft.ML" .`)
- [ ] FDM calculation returns ≥300 THB
- [ ] SLA calculation returns ≥500 THB
- [ ] CNC calculation returns ≥2500 THB
- [ ] Identical requests return identical prices

---

## Common Issues

### "No calculator for technology X"

Ensure all calculators are registered in DI and return the correct `Technology` property value.

### MaterialService returns 404

Verify MaterialService is running and material IDs in requests are valid.

### Price below minimum floor

Check that `Math.Max(result, minimumPrice)` is applied as the final step in each calculator.

---

## Next Steps

1. Run `/speckit.tasks` to generate detailed task list
2. Implement unit tests for each calculator
3. Add EF Core migration for `PricingAuditRecord` changes
