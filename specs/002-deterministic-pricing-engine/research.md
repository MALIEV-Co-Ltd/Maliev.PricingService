# Research: Deterministic Pricing Engine

**Feature**: 002-deterministic-pricing-engine  
**Date**: 2026-02-22

## Research Questions

### Q1: What pattern should calculators use?

**Decision**: Strategy Pattern via `IPricingCalculator` interface

**Rationale**: 
- Each manufacturing technology has distinct calculation logic
- Strategy pattern allows independent testing of each calculator
- Easy to add new technologies without modifying existing code
- DI container can resolve correct calculator by `Technology` property

**Alternatives Considered**:
| Alternative | Rejected Because |
|-------------|------------------|
| Single calculator with switch/if-else | Violates Open/Closed Principle, harder to test |
| Factory pattern returning delegates | Less type-safe, harder to inject dependencies |
| Chain of responsibility | Overkill for simple selection logic |

---

### Q2: How should calculators be resolved at runtime?

**Decision**: DI-injected `IEnumerable<IPricingCalculator>` with LINQ lookup

**Rationale**:
- ASP.NET Core natively supports injecting all implementations of an interface
- Each calculator exposes its `Technology` property for identification
- Simple one-line lookup: `_calculators.FirstOrDefault(c => c.Technology == request.Technology)`
- No additional factory infrastructure needed

**Alternatives Considered**:
| Alternative | Rejected Because |
|-------------|------------------|
| Dictionary-based factory | Requires manual registration, more boilerplate |
| Keyed services (new in .NET 8) | Less intuitive for this use case, adds complexity |
| Service locator pattern | Anti-pattern, harder to test |

---

### Q3: Where should machine rates be stored?

**Decision**: `appsettings.json` with `IOptions<MachineRates>` pattern

**Rationale**:
- Rates change infrequently but need adjustment capability
- No database migration required
- Follows .NET configuration best practices
- Can be overridden per environment (dev/staging/prod)
- Rates can be hot-reloaded if needed

**Alternatives Considered**:
| Alternative | Rejected Because |
|-------------|------------------|
| Database table | Adds deployment complexity, overkill for static rates |
| Environment variables | Harder to structure, no nested configuration |
| Hardcoded constants | Not configurable without code changes |

---

### Q4: How should material data be obtained?

**Decision**: HTTP call to MaterialService via existing `IMaterialServiceClient`

**Rationale**:
- Aligns with existing microservice architecture
- MaterialService is already the source of truth for material data
- Existing client infrastructure can be extended
- No data duplication

**Alternatives Considered**:
| Alternative | Rejected Because |
|-------------|------------------|
| Denormalized in PricingService | Data duplication, sync issues |
| Cache-first with background refresh | Adds complexity, material data changes rarely |

---

### Q5: How should ProcessParameters be extracted?

**Decision**: Dictionary with decimal.TryParse, default values on failure

**Rationale**:
- ProcessParameters is a `Dictionary<string, string>` from MaterialService
- Calculators extract needed values with reasonable defaults
- Missing parameters don't crash the calculation
- Logs warning if expected parameter is missing

**Implementation Pattern**:
```csharp
private static decimal GetParam(Dictionary<string, string> params, string key, decimal defaultValue)
{
    if (params.TryGetValue(key, out var value) && decimal.TryParse(value, out var result))
        return result;
    return defaultValue;
}
```

---

### Q6: How should FDM minimum layer time work?

**Decision**: Apply `Math.Max(calculatedLayerTime, minLayerTime)` per spec

**Rationale**:
- Prevents unrealistically fast print time estimates for sparse geometries
- Matches real-world 3D printer behavior (layer cooling requirements)
- Simple max comparison, no complex logic

**Example**:
- Calculated: 0.5 seconds/layer (hollow basket)
- MinLayerTime: 5 seconds/layer
- Effective: 5 seconds/layer

---

### Q7: How should CNC complexity factor work?

**Decision**: Surface-area-to-volume ratio as multiplier on machine time cost

**Rationale**:
- High SA:V ratio = more tool paths = more machine time
- Simple formula: `complexityFactor = SurfaceAreaCm2 / VolumeCm3`
- Applied as multiplier to machine time cost, not base rate

**Example**:
- Solid cube 10×10×10cm: SA:V = 600/1000 = 0.6
- Hollow shell: SA:V could be 5.0+
- Higher factor = proportionally higher machine time cost

---

### Q8: How should Scanning/Design note be included?

**Decision**: Add `Notes` property to `PricingResult` record

**Rationale**:
- Notes field already conceptually exists in spec
- Consumers can display notes to users
- Nullable property, empty for FDM/SLA/CNC

**Implementation**:
```csharp
public record PricingResult
{
    // ... existing properties ...
    public string? Notes { get; init; }
}
```

---

## Dependency Verification

### MaterialService Fields Required

| Field | Type | Purpose |
|-------|------|---------|
| `Density` | decimal (g/cm³) | Weight calculation |
| `CostPerKg` | decimal (THB/kg) | Material cost |
| `ProcessParameters` | Dictionary<string, string> | Technology-specific settings |

**Verification Status**: ⚠️ Pending

Action: Verify MaterialService API contract before implementation. If fields are missing, coordinate with MaterialService team or this feature blocks.

---

## External References

- [Strategy Pattern in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-service-registration#service-registration-methods)
- [IOptions Pattern](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- Existing `RuleBasedPricingEngine.cs` for formula context
