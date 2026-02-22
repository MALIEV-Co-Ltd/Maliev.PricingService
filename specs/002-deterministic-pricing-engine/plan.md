# Implementation Plan: Deterministic Pricing Engine

**Branch**: `002-deterministic-pricing-engine` | **Date**: 2026-02-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-deterministic-pricing-engine/spec.md`

## Summary

Replace the existing ML-based pricing engine (60-70% accuracy) with five deterministic, technology-specific calculators (FDM, SLA, CNC, Scanning, Design) that guarantee identical prices for identical inputs. Remove ML.net dependencies and refactor the `RuleBasedPricingEngine` to delegate to calculator implementations via strategy pattern.

## Technical Context

**Language/Version**: C# 13 / .NET 10  
**Primary Dependencies**: ASP.NET Core, EF Core 10, MassTransit, Microsoft.Extensions.Options  
**Storage**: PostgreSQL (via EF Core)  
**Testing**: xUnit, Moq, Testcontainers  
**Target Platform**: Linux container (Aspire orchestration)  
**Project Type**: Web API microservice  
**Performance Goals**: Under 1 second per calculation (SC-007)  
**Constraints**: Deterministic pricing (zero variance for identical inputs)  
**Scale/Scope**: Single service, ~15 files modified/created

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Status**: ✅ PASS

The project constitution is a template without defined constraints. No violations detected.

## Project Structure

### Documentation (this feature)

```text
specs/002-deterministic-pricing-engine/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── checklists/          # Validation checklists
```

### Source Code (repository root)

```text
Maliev.PricingService.Api/
├── Services/
│   ├── PricingModels.cs              # UPDATE: Add ManufacturingTechnology, MaterialData, MachineRates
│   ├── PricingOrchestrator.cs        # UPDATE: Remove config lookup, add MaterialClient integration
│   ├── RuleBasedPricingEngine.cs     # REFACTOR: Delegate to calculators
│   ├── IPricingEngine.cs             # UPDATE: Change signature (remove PricingConfiguration)
│   ├── IMLPricingEngine.cs           # DELETE
│   ├── MLPricingEngine.cs            # DELETE
│   └── Calculators/                  # CREATE
│       ├── IPricingCalculator.cs
│       ├── FdmPricingCalculator.cs
│       ├── SlaPricingCalculator.cs
│       ├── CncPricingCalculator.cs
│       ├── ScanningPricingCalculator.cs
│       └── DesignPricingCalculator.cs
├── Clients/
│   ├── IMaterialServiceClient.cs     # UPDATE: Add ProcessParameters to MaterialDto
│   └── MaterialServiceClient.cs      # UPDATE: Map new fields
├── Consumers/
│   └── FileAnalyzedEventConsumer.cs  # UPDATE: Add Technology defaults
├── Program.cs                        # UPDATE: Register calculators, MachineRates options
└── appsettings.json                  # UPDATE: Add Pricing:MachineRates section

Maliev.PricingService.Data/
└── Entities/
    └── PricingAuditRecord.cs         # UPDATE: Add Technology, remove ML fields

Maliev.PricingService.Tests/
├── Unit/
│   └── Calculators/                  # CREATE: Unit tests for each calculator
└── Integration/
    └── PricingIntegrationTests.cs    # CREATE: Integration test with mocked MaterialService
```

**Structure Decision**: Existing microservice structure preserved. New `Calculators/` subdirectory for strategy implementations. No new projects required.

## Complexity Tracking

> No constitution violations to justify.

---

## Phase 0: Research Summary

See [research.md](./research.md) for full details.

### Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Calculator pattern | Strategy via `IPricingCalculator` | Clean separation, easy to test, extensible |
| Calculator resolution | DI-injected `IEnumerable<IPricingCalculator>` | Simple lookup by `Technology` property |
| Machine rates storage | `appsettings.json` + `IOptions<MachineRates>` | Configurable without DB migration |
| Material data source | MaterialService API call | Aligns with existing architecture |

### Dependencies

| Dependency | Status | Notes |
|------------|--------|-------|
| MaterialService `Density` field | ⚠️ Required | Must verify before implementation |
| MaterialService `CostPerKg` field | ⚠️ Required | Must verify before implementation |
| MaterialService `ProcessParameters` field | ⚠️ Required | Must verify before implementation |

---

## Phase 1: Data Model

See [data-model.md](./data-model.md) for full details.

### New Types

| Type | Purpose |
|------|---------|
| `ManufacturingTechnology` | Enum: Fdm=1, Sla=2, Cnc=3, Scanning=4, Design=5 |
| `MaterialData` | Material properties extracted from MaterialService DTO |
| `MachineRates` | Hourly rates and setup fees from configuration |

### Modified Types

| Type | Changes |
|------|---------|
| `PricingRequest` | Add Technology, LayerHeightMm, SupportEnabled, HeightMm, ScanningTier |
| `PricingResult` | Add Notes field for manual-confirmation scenarios |
| `PricingAuditRecord` | Add Technology, remove MLModelVersion, ConfigMaterialPricePerCm3, ConfigSupportPricePerCm3 |
| `PricingStrategy` | Remove MLEnhanced=2, Hybrid=4 |

---

## Phase 2: Contracts

See [contracts/](./contracts/) for API specifications.

### Pricing Request Schema

```json
{
  "fileId": "guid",
  "customerId": "guid",
  "materialId": "guid",
  "technology": "Fdm | Sla | Cnc | Scanning | Design",
  "layerHeightMm": 0.2,
  "supportEnabled": false,
  "heightMm": 100.0,
  "scanningTier": "RawScan | ReverseEngineering",
  "quantity": 1,
  "geometry": { "volumeCm3": 10.0, "surfaceAreaCm2": 50.0, ... }
}
```

---

## Implementation Phases

### Phase A: Cleanup (Remove ML Engine)
1. Delete `IMLPricingEngine.cs`, `MLPricingEngine.cs`
2. Remove ML.net packages from `.csproj`
3. Remove `MLEnhanced`, `Hybrid` from `PricingStrategy` enum
4. Rename `PrintingTechnology` → `ManufacturingTechnology` throughout codebase (FR-016)
5. Run `dotnet build` to identify compilation errors

### Phase B: New Models
1. Add `ManufacturingTechnology` enum to `PricingModels.cs`
2. Add `MaterialData` and `MachineRates` records
3. Update `PricingRequest` with new fields
4. Update `MaterialDto` with `ProcessParameters`

### Phase C: Calculator Implementation
1. Create `Services/Calculators/` directory
2. Create `IPricingCalculator` interface
3. Implement `FdmPricingCalculator` (minimum 300 THB)
4. Implement `SlaPricingCalculator` (minimum 500 THB)
5. Implement `CncPricingCalculator` (minimum 2500 THB)
6. Implement `ScanningPricingCalculator` (2500/4500 THB tiers)
7. Implement `DesignPricingCalculator` (500 THB minimum)

### Phase D: Engine Refactor
1. Update `IPricingEngine.CalculateAsync` signature
2. Refactor `RuleBasedPricingEngine` to delegate to calculators
3. Update `PricingOrchestrator` to use MaterialClient + IOptions

### Phase E: Configuration & DI
1. Add `Pricing:MachineRates` to `appsettings.json`
2. Register `IOptions<MachineRates>` in `Program.cs`
3. Register all calculators in DI container
4. Update `FileAnalyzedEventConsumer` defaults

### Phase F: Testing
1. Unit tests for each calculator
2. Determinism test (same inputs → same outputs)
3. Minimum floor tests
4. Performance benchmark test (SC-007: under 1 second)
5. Integration test with MaterialService mock

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| MaterialService missing required fields | Medium | High | Verify dependency before starting; coordinate with MaterialService team |
| Pricing formulas produce unexpected results | Low | Medium | Unit tests with known inputs/outputs from spec |
| Breaking change to existing consumers | Low | High | ManufacturingProcessId remains optional for backwards compatibility |

---

## Next Steps

1. Run `/speckit.tasks` to generate task breakdown
2. Verify MaterialService dependency (Density, CostPerKg, ProcessParameters)
3. Begin Phase A: Cleanup
