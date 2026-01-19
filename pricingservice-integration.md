# Maliev.PricingService Integration Plan

> **Purpose**: Detailed implementation guide for creating the PricingService microservice for instant quotation capabilities with full audit trail.
> **Target Audience**: LLM coding assistants and developers
> **Status**: Ready for implementation

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Pre-Implementation Checklist](#pre-implementation-checklist)
4. [Phase 1: Create PricingService Foundation](#phase-1-create-pricingservice-foundation)
5. [Phase 2: Implement Data Model](#phase-2-implement-data-model)
6. [Phase 3: Implement Pricing Engines](#phase-3-implement-pricing-engines)
7. [Phase 4: Implement API Controllers](#phase-4-implement-api-controllers)
8. [Phase 5: Implement Event Consumers](#phase-5-implement-event-consumers)
9. [Phase 6: Update MessagingContracts](#phase-6-update-messagingcontracts)
10. [Phase 7: Update QuotationService Integration](#phase-7-update-quotationservice-integration)
11. [Phase 8: Historical Data Migration](#phase-8-historical-data-migration)
12. [Phase 9: Testing](#phase-9-testing)
13. [Phase 10: GitOps & Deployment](#phase-10-gitops--deployment)
14. [Verification Checklist](#verification-checklist)

---

## Executive Summary

### Objective
Create a new **Maliev.PricingService** microservice that:
1. Receives geometry analysis data from GeometryService
2. Calculates instant prices using rule-based and ML algorithms
3. Maintains complete audit trail for all pricing calculations
4. Publishes pricing events for QuotationService to consume

### Key Decisions
- **New service**: Create `Maliev.PricingService` (do NOT rename PredictionService)
- **Keep PredictionService**: Reserved for future non-pricing ML features
- **QuotationService**: Remains pure CRUD (no pricing logic)
- **Audit trail**: Every calculation logged with full context (7-year retention)
- **Historical data**: Import from existing PostgreSQL database

### Event Flow
```
FileUploadedEvent (UploadService)
         ↓
FileAnalyzedEvent (GeometryService - Python)
         ↓
PricingService consumes → calculates price → creates audit record
         ↓
PriceCalculatedEvent (PricingService)
         ↓
QuotationService consumes → creates draft quotation
```

---

## Architecture Overview

### Service Boundaries

| Service | Responsibility | Database | Does NOT Do |
|---------|---------------|----------|-------------|
| **PricingService** (NEW) | Price calculation, ML models, audit trail | `pricing_app_db` | Store quotations, file handling |
| **QuotationService** | CRUD for quotations, line items, status | `quotation_app_db` | Pricing calculations |
| **GeometryService** | Extract 3D metrics from files | N/A (stateless) | Pricing, file storage |
| **MaterialService** | Material catalog, base prices | `material_app_db` | Quote creation |
| **PredictionService** | Future: demand forecasting, recommendations | `prediction_app_db` | Pricing (reserved for other ML) |

### Project Structure

```
B:\maliev\Maliev.PricingService\
├── Maliev.PricingService.Api\
│   ├── Controllers\
│   │   └── v1\
│   │       ├── PricingController.cs
│   │       ├── ConfigurationsController.cs
│   │       └── ModelsController.cs
│   ├── Services\
│   │   ├── IPricingEngine.cs
│   │   ├── RuleBasedPricingEngine.cs
│   │   ├── MLPricingEngine.cs
│   │   └── PricingOrchestrator.cs
│   ├── Consumers\
│   │   ├── FileAnalyzedEventConsumer.cs
│   │   └── OrderCompletedEventConsumer.cs
│   ├── Clients\
│   │   ├── IMaterialServiceClient.cs
│   │   ├── MaterialServiceClient.cs
│   │   ├── ICurrencyServiceClient.cs
│   │   └── CurrencyServiceClient.cs
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Dockerfile
├── Maliev.PricingService.Data\
│   ├── PricingDbContext.cs
│   ├── Entities\
│   │   ├── PricingConfiguration.cs
│   │   ├── PricingAuditRecord.cs
│   │   ├── PricingModel.cs
│   │   └── PricingTrainingData.cs
│   ├── Configurations\
│   │   ├── PricingConfigurationEntityConfiguration.cs
│   │   ├── PricingAuditRecordEntityConfiguration.cs
│   │   ├── PricingModelEntityConfiguration.cs
│   │   └── PricingTrainingDataEntityConfiguration.cs
│   └── Migrations\
├── Maliev.PricingService.Tests\
│   ├── Integration\
│   │   ├── PricingControllerTests.cs
│   │   ├── FileAnalyzedEventConsumerTests.cs
│   │   └── PricingEngineTests.cs
│   └── TestFixtures\
│       └── PricingServiceTestFactory.cs
├── Maliev.PricingService.sln
├── nuget.config
├── .pre-commit-config.yaml
└── README.md
```

---

## Pre-Implementation Checklist

### Prerequisites
- [ ] .NET 10.0 SDK installed
- [ ] Docker Desktop running
- [ ] Access to Maliev.Aspire.ServiceDefaults NuGet package
- [ ] Access to Maliev.MessagingContracts NuGet package
- [ ] PostgreSQL 18 available for testing

### Required Information
- [ ] Material pricing data (from MaterialService)
- [ ] Historical quote/order data (from legacy PostgreSQL)
- [ ] Currency conversion rates (from CurrencyService)
- [ ] IAM permission structure confirmed

---

## Phase 1: Create PricingService Foundation

### Task 1.1: Create Solution and Projects

**Location**: `B:\maliev\Maliev.PricingService\`

**Commands**:
```bash
cd B:\maliev
mkdir Maliev.PricingService
cd Maliev.PricingService

# Create solution
dotnet new sln -n Maliev.PricingService

# Create API project
dotnet new webapi -n Maliev.PricingService.Api -f net10.0
dotnet sln add Maliev.PricingService.Api

# Create Data project
dotnet new classlib -n Maliev.PricingService.Data -f net10.0
dotnet sln add Maliev.PricingService.Data

# Create Tests project
dotnet new xunit -n Maliev.PricingService.Tests -f net10.0
dotnet sln add Maliev.PricingService.Tests

# Add project references
dotnet add Maliev.PricingService.Api reference Maliev.PricingService.Data
dotnet add Maliev.PricingService.Tests reference Maliev.PricingService.Api
dotnet add Maliev.PricingService.Tests reference Maliev.PricingService.Data
```

**Checklist**:
- [ ] Solution file created
- [ ] Api project created
- [ ] Data project created
- [ ] Tests project created
- [ ] Project references configured

### Task 1.2: Configure NuGet Packages

**File**: `Maliev.PricingService.Api/Maliev.PricingService.Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Use ProjectReference for local development, CI replaces with PackageReference -->
    <ProjectReference Include="..\..\Maliev.Aspire\Maliev.Aspire.ServiceDefaults\Maliev.Aspire.ServiceDefaults.csproj" />
    <ProjectReference Include="..\..\Maliev.MessagingContracts\Maliev.MessagingContracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.ML" Version="4.0.0" />
    <PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.20.0" />
  </ItemGroup>
</Project>
```

**File**: `Maliev.PricingService.Data/Maliev.PricingService.Data.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
  </ItemGroup>
</Project>
```

**Checklist**:
- [ ] Api project packages configured
- [ ] Data project packages configured
- [ ] TreatWarningsAsErrors enabled
- [ ] GenerateDocumentationFile enabled

### Task 1.3: Create nuget.config

**File**: `Maliev.PricingService/nuget.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="maliev-packages" value="https://nuget.pkg.github.com/MALIEV-Co-Ltd/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <maliev-packages>
      <add key="Username" value="MALIEV-Co-Ltd" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </maliev-packages>
  </packageSourceCredentials>
</configuration>
```

**Checklist**:
- [ ] nuget.config created
- [ ] GitHub packages source configured

---

## Phase 2: Implement Data Model

### Task 2.1: Create PricingConfiguration Entity

**File**: `Maliev.PricingService.Data/Entities/PricingConfiguration.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Data.Entities;

/// <summary>
/// Configuration for pricing rules per material and manufacturing process combination.
/// Supports temporal versioning with effective dates.
/// </summary>
public class PricingConfiguration
{
    /// <summary>
    /// Unique identifier for the pricing configuration.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the material in MaterialService.
    /// </summary>
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Reference to the manufacturing process in MaterialService.
    /// </summary>
    [Required]
    public Guid ManufacturingProcessId { get; set; }

    /// <summary>
    /// Material cost per cubic centimeter.
    /// </summary>
    [Range(0.0001, 100000)]
    public decimal MaterialPricePerCm3 { get; set; }

    /// <summary>
    /// Support material cost per cubic centimeter.
    /// </summary>
    [Range(0, 100000)]
    public decimal SupportMaterialPricePerCm3 { get; set; }

    /// <summary>
    /// Material density in grams per cubic centimeter.
    /// </summary>
    [Range(0.01, 50)]
    public decimal DensityGramPerCm3 { get; set; }

    /// <summary>
    /// Machine operating cost per hour.
    /// </summary>
    [Range(0, 100000)]
    public decimal MachineHourlyRate { get; set; }

    /// <summary>
    /// Printing speed in cubic centimeters per hour.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal PrintSpeedCm3PerHour { get; set; }

    /// <summary>
    /// One-time setup cost per job.
    /// </summary>
    [Range(0, 100000)]
    public decimal SetupCostFlat { get; set; }

    /// <summary>
    /// Minimum order price regardless of calculations.
    /// </summary>
    [Range(0, 1000000)]
    public decimal MinimumOrderPrice { get; set; }

    /// <summary>
    /// Margin multiplier (e.g., 1.5 = 50% margin).
    /// </summary>
    [Range(1.0, 10.0)]
    public decimal MarginMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Surface area to volume ratio threshold for complexity surcharge.
    /// </summary>
    [Range(0.1, 100)]
    public decimal ComplexityThreshold { get; set; } = 2.0m;

    /// <summary>
    /// Percentage surcharge for complex parts.
    /// </summary>
    [Range(0, 100)]
    public decimal ComplexitySurchargePercent { get; set; } = 15m;

    /// <summary>
    /// Date from which this configuration is effective.
    /// </summary>
    [Required]
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Date until which this configuration is effective. Null means no end date.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Whether this configuration is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User ID who created the configuration.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the configuration was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User ID who last updated the configuration.
    /// </summary>
    [StringLength(100)]
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
```

**Checklist**:
- [ ] Entity created with all properties
- [ ] XML documentation on all public members
- [ ] Data annotations for validation
- [ ] RowVersion for optimistic concurrency

### Task 2.2: Create PricingAuditRecord Entity

**File**: `Maliev.PricingService.Data/Entities/PricingAuditRecord.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Data.Entities;

/// <summary>
/// Immutable audit record of every pricing calculation.
/// Never updated after creation - provides complete traceability.
/// </summary>
public class PricingAuditRecord
{
    /// <summary>
    /// Unique identifier for the audit record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the quotation created from this pricing (set after quotation creation).
    /// </summary>
    public Guid? QuotationId { get; set; }

    /// <summary>
    /// Reference to the analyzed file.
    /// </summary>
    [Required]
    public Guid FileId { get; set; }

    /// <summary>
    /// Reference to the customer requesting the quote.
    /// </summary>
    [Required]
    public Guid CustomerId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Input: Geometry Metrics (snapshot at time of calculation)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Part volume in cubic centimeters.
    /// </summary>
    [Range(0.001, 1000000)]
    public decimal InputVolumeCm3 { get; set; }

    /// <summary>
    /// Estimated support volume in cubic centimeters.
    /// </summary>
    [Range(0, 1000000)]
    public decimal InputSupportVolumeCm3 { get; set; }

    /// <summary>
    /// Surface area in square centimeters.
    /// </summary>
    [Range(0.01, 10000000)]
    public decimal InputSurfaceAreaCm2 { get; set; }

    /// <summary>
    /// Bounding box X dimension in millimeters.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal InputBoundingBoxX { get; set; }

    /// <summary>
    /// Bounding box Y dimension in millimeters.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal InputBoundingBoxY { get; set; }

    /// <summary>
    /// Bounding box Z dimension in millimeters.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal InputBoundingBoxZ { get; set; }

    /// <summary>
    /// Whether the mesh is watertight/manifold.
    /// </summary>
    public bool InputIsManifold { get; set; }

    /// <summary>
    /// Number of triangles in the mesh.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int InputTriangleCount { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Input: Material and Process
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reference to the material in MaterialService.
    /// </summary>
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Material code at time of calculation (denormalized for audit).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the manufacturing process in MaterialService.
    /// </summary>
    [Required]
    public Guid ManufacturingProcessId { get; set; }

    /// <summary>
    /// Manufacturing process name at time of calculation (denormalized for audit).
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ManufacturingProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of parts requested.
    /// </summary>
    [Range(1, 1000000)]
    public int Quantity { get; set; } = 1;

    // ─────────────────────────────────────────────────────────────
    // Pricing Configuration Snapshot
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reference to the pricing configuration used.
    /// </summary>
    [Required]
    public Guid PricingConfigurationId { get; set; }

    /// <summary>
    /// Material price per cm3 at time of calculation.
    /// </summary>
    public decimal ConfigMaterialPricePerCm3 { get; set; }

    /// <summary>
    /// Support material price per cm3 at time of calculation.
    /// </summary>
    public decimal ConfigSupportPricePerCm3 { get; set; }

    /// <summary>
    /// Machine hourly rate at time of calculation.
    /// </summary>
    public decimal ConfigMachineHourlyRate { get; set; }

    /// <summary>
    /// Margin multiplier at time of calculation.
    /// </summary>
    public decimal ConfigMarginMultiplier { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Strategy Used
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Pricing strategy used for this calculation.
    /// </summary>
    [Required]
    public PricingStrategy Strategy { get; set; }

    /// <summary>
    /// ML model version if ML strategy was used.
    /// </summary>
    [StringLength(50)]
    public string? MLModelVersion { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Output: Price Breakdown
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Cost of material for the part.
    /// </summary>
    [Range(0, 10000000)]
    public decimal MaterialCost { get; set; }

    /// <summary>
    /// Cost of support material.
    /// </summary>
    [Range(0, 10000000)]
    public decimal SupportMaterialCost { get; set; }

    /// <summary>
    /// Cost of machine time.
    /// </summary>
    [Range(0, 10000000)]
    public decimal MachineTimeCost { get; set; }

    /// <summary>
    /// One-time setup cost.
    /// </summary>
    [Range(0, 10000000)]
    public decimal SetupCost { get; set; }

    /// <summary>
    /// Surcharge for complex geometry.
    /// </summary>
    [Range(0, 10000000)]
    public decimal ComplexitySurcharge { get; set; }

    /// <summary>
    /// Subtotal before margin is applied.
    /// </summary>
    [Range(0, 100000000)]
    public decimal SubtotalBeforeMargin { get; set; }

    /// <summary>
    /// Margin amount added.
    /// </summary>
    [Range(0, 100000000)]
    public decimal MarginAmount { get; set; }

    /// <summary>
    /// Final price per unit.
    /// </summary>
    [Range(0, 100000000)]
    public decimal TotalUnitPrice { get; set; }

    /// <summary>
    /// Total price (TotalUnitPrice × Quantity).
    /// </summary>
    [Range(0, 1000000000)]
    public decimal TotalPrice { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Confidence and Validity
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Confidence level of the price estimate (0.0 - 1.0).
    /// </summary>
    [Range(0, 1)]
    public decimal ConfidenceLevel { get; set; }

    /// <summary>
    /// Currency code for the price.
    /// </summary>
    [Required]
    [StringLength(3)]
    public string CurrencyCode { get; set; } = "THB";

    /// <summary>
    /// Date from which this price is valid.
    /// </summary>
    [Required]
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// Date until which this price is valid.
    /// </summary>
    [Required]
    public DateTime ValidUntil { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Audit Metadata
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Timestamp when the calculation was performed.
    /// </summary>
    [Required]
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// System that performed the calculation.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string CalculatedBySystem { get; set; } = "PricingService";

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    [StringLength(100)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Time taken to perform the calculation.
    /// </summary>
    [Required]
    public TimeSpan CalculationDuration { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation Properties
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigation to the pricing configuration used.
    /// </summary>
    public PricingConfiguration? PricingConfiguration { get; set; }

    /// <summary>
    /// Navigation to training data derived from this record.
    /// </summary>
    public PricingTrainingData? TrainingData { get; set; }
}

/// <summary>
/// Pricing strategy used for calculations.
/// </summary>
public enum PricingStrategy
{
    /// <summary>
    /// Deterministic rule-based calculation.
    /// </summary>
    RuleBased = 1,

    /// <summary>
    /// Machine learning enhanced calculation.
    /// </summary>
    MLEnhanced = 2,

    /// <summary>
    /// Manual pricing by staff.
    /// </summary>
    Manual = 3,

    /// <summary>
    /// Combination of rule-based with ML adjustments.
    /// </summary>
    Hybrid = 4
}
```

**Checklist**:
- [ ] Entity created with all properties
- [ ] Immutable design (no Update methods)
- [ ] Complete geometry input fields
- [ ] Complete price breakdown fields
- [ ] Audit metadata fields
- [ ] XML documentation on all members

### Task 2.3: Create PricingModel Entity

**File**: `Maliev.PricingService.Data/Entities/PricingModel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Data.Entities;

/// <summary>
/// Tracks ML model training and deployment for pricing predictions.
/// </summary>
public class PricingModel
{
    /// <summary>
    /// Unique identifier for the model.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name for the model.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Version string (e.g., "1.0.0", "2.1.0-beta").
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Type of pricing model.
    /// </summary>
    [Required]
    public PricingModelType ModelType { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Training Metadata
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Number of training samples used.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TrainingDataCount { get; set; }

    /// <summary>
    /// When training started.
    /// </summary>
    [Required]
    public DateTime TrainingStartedAt { get; set; }

    /// <summary>
    /// When training completed.
    /// </summary>
    [Required]
    public DateTime TrainingCompletedAt { get; set; }

    /// <summary>
    /// Total training duration.
    /// </summary>
    [Required]
    public TimeSpan TrainingDuration { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Model Performance Metrics
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Mean Absolute Error of predictions.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal MeanAbsoluteError { get; set; }

    /// <summary>
    /// Mean Absolute Percentage Error of predictions.
    /// </summary>
    [Range(0, 100)]
    public decimal MeanAbsolutePercentageError { get; set; }

    /// <summary>
    /// R-squared (coefficient of determination).
    /// </summary>
    [Range(0, 1)]
    public decimal RSquared { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Deployment Status
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this model is currently active for predictions.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the model was deployed to production.
    /// </summary>
    public DateTime? DeployedAt { get; set; }

    /// <summary>
    /// When the model was retired from production.
    /// </summary>
    public DateTime? RetiredAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Storage
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Path to the ONNX model file in storage.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string ModelFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Type of ML model for pricing.
/// </summary>
public enum PricingModelType
{
    /// <summary>
    /// Optimizes price for conversion and profit.
    /// </summary>
    PriceOptimization = 1,

    /// <summary>
    /// Estimates actual production cost.
    /// </summary>
    CostEstimation = 2,

    /// <summary>
    /// Predicts manufacturing success rate.
    /// </summary>
    SuccessPrediction = 3
}
```

**Checklist**:
- [ ] Entity created with all properties
- [ ] Training metadata fields
- [ ] Performance metric fields
- [ ] Deployment status tracking
- [ ] XML documentation on all members

### Task 2.4: Create PricingTrainingData Entity

**File**: `Maliev.PricingService.Data/Entities/PricingTrainingData.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Data.Entities;

/// <summary>
/// Training data collected from completed jobs for ML model improvement.
/// Links pricing audit records to actual job outcomes.
/// </summary>
public class PricingTrainingData
{
    /// <summary>
    /// Unique identifier for the training data record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the pricing audit record.
    /// </summary>
    [Required]
    public Guid PricingAuditRecordId { get; set; }

    /// <summary>
    /// Reference to the order created from this quote.
    /// </summary>
    public Guid? OrderId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Outcome Tracking
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the customer accepted the quote.
    /// </summary>
    public bool CustomerAccepted { get; set; }

    /// <summary>
    /// When the customer accepted the quote.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Whether the job was completed.
    /// </summary>
    public bool JobCompleted { get; set; }

    /// <summary>
    /// When the job was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Whether the manufacturing succeeded (no reprints needed).
    /// </summary>
    public bool JobSucceeded { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Actual Costs (filled after job completion)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Actual material used in cubic centimeters.
    /// </summary>
    [Range(0, 1000000)]
    public decimal? ActualMaterialUsedCm3 { get; set; }

    /// <summary>
    /// Actual print time in hours.
    /// </summary>
    [Range(0, 10000)]
    public decimal? ActualPrintTimeHours { get; set; }

    /// <summary>
    /// Actual labor hours spent.
    /// </summary>
    [Range(0, 10000)]
    public decimal? ActualLaborHours { get; set; }

    /// <summary>
    /// Actual total cost of the job.
    /// </summary>
    [Range(0, 100000000)]
    public decimal? ActualTotalCost { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Profit Analysis
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Actual profit margin: (QuotedPrice - ActualCost) / QuotedPrice.
    /// </summary>
    [Range(-10, 1)]
    public decimal? ActualProfitMargin { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Training Status
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this record has been used for model training.
    /// </summary>
    public bool UsedForTraining { get; set; }

    /// <summary>
    /// Reference to the model trained using this data.
    /// </summary>
    public Guid? TrainedModelId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation Properties
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigation to the pricing audit record.
    /// </summary>
    public PricingAuditRecord? PricingAuditRecord { get; set; }

    /// <summary>
    /// Navigation to the model trained using this data.
    /// </summary>
    public PricingModel? TrainedModel { get; set; }
}
```

**Checklist**:
- [ ] Entity created with all properties
- [ ] Outcome tracking fields
- [ ] Actual cost fields
- [ ] Profit analysis fields
- [ ] Training status tracking
- [ ] XML documentation on all members

### Task 2.5: Create PricingDbContext

**File**: `Maliev.PricingService.Data/PricingDbContext.cs`

```csharp
using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Data;

/// <summary>
/// Database context for the Pricing Service.
/// </summary>
public class PricingDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PricingDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the context.</param>
    public PricingDbContext(DbContextOptions<PricingDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the pricing configurations.
    /// </summary>
    public DbSet<PricingConfiguration> PricingConfigurations => Set<PricingConfiguration>();

    /// <summary>
    /// Gets or sets the pricing audit records.
    /// </summary>
    public DbSet<PricingAuditRecord> PricingAuditRecords => Set<PricingAuditRecord>();

    /// <summary>
    /// Gets or sets the pricing models.
    /// </summary>
    public DbSet<PricingModel> PricingModels => Set<PricingModel>();

    /// <summary>
    /// Gets or sets the pricing training data.
    /// </summary>
    public DbSet<PricingTrainingData> PricingTrainingData => Set<PricingTrainingData>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);
    }
}
```

**Checklist**:
- [ ] DbContext created
- [ ] All DbSet properties defined
- [ ] Configuration assembly loading
- [ ] XML documentation

### Task 2.6: Create Entity Configurations

**File**: `Maliev.PricingService.Data/Configurations/PricingConfigurationEntityConfiguration.cs`

```csharp
using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PricingService.Data.Configurations;

/// <summary>
/// Entity configuration for PricingConfiguration.
/// </summary>
public class PricingConfigurationEntityConfiguration : IEntityTypeConfiguration<PricingConfiguration>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PricingConfiguration> builder)
    {
        builder.ToTable("pricing_configurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.MaterialId)
            .HasColumnName("material_id")
            .IsRequired();

        builder.Property(x => x.ManufacturingProcessId)
            .HasColumnName("manufacturing_process_id")
            .IsRequired();

        builder.Property(x => x.MaterialPricePerCm3)
            .HasColumnName("material_price_per_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.SupportMaterialPricePerCm3)
            .HasColumnName("support_material_price_per_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.DensityGramPerCm3)
            .HasColumnName("density_gram_per_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.MachineHourlyRate)
            .HasColumnName("machine_hourly_rate")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PrintSpeedCm3PerHour)
            .HasColumnName("print_speed_cm3_per_hour")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.SetupCostFlat)
            .HasColumnName("setup_cost_flat")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(x => x.MinimumOrderPrice)
            .HasColumnName("minimum_order_price")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(x => x.MarginMultiplier)
            .HasColumnName("margin_multiplier")
            .HasPrecision(5, 2)
            .HasDefaultValue(1.5m);

        builder.Property(x => x.ComplexityThreshold)
            .HasColumnName("complexity_threshold")
            .HasPrecision(5, 2)
            .HasDefaultValue(2.0m);

        builder.Property(x => x.ComplexitySurchargePercent)
            .HasColumnName("complexity_surcharge_percent")
            .HasPrecision(5, 2)
            .HasDefaultValue(15m);

        builder.Property(x => x.EffectiveFrom)
            .HasColumnName("effective_from")
            .IsRequired();

        builder.Property(x => x.EffectiveTo)
            .HasColumnName("effective_to");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // Unique constraint: one active configuration per material+process at any time
        builder.HasIndex(x => new { x.MaterialId, x.ManufacturingProcessId, x.EffectiveFrom })
            .IsUnique();

        // Index for active lookups
        builder.HasIndex(x => new { x.MaterialId, x.ManufacturingProcessId, x.IsActive });
    }
}
```

**Note**: Create similar configuration files for `PricingAuditRecord`, `PricingModel`, and `PricingTrainingData`.

**Checklist**:
- [ ] PricingConfigurationEntityConfiguration created
- [ ] PricingAuditRecordEntityConfiguration created
- [ ] PricingModelEntityConfiguration created
- [ ] PricingTrainingDataEntityConfiguration created
- [ ] All snake_case column names
- [ ] Proper indexes defined
- [ ] RowVersion configured

---

## Phase 3: Implement Pricing Engines

### Task 3.1: Create IPricingEngine Interface

**File**: `Maliev.PricingService.Api/Services/IPricingEngine.cs`

```csharp
using Maliev.PricingService.Data.Entities;

namespace Maliev.PricingService.Api.Services;

/// <summary>
/// Interface for pricing calculation engines.
/// </summary>
public interface IPricingEngine
{
    /// <summary>
    /// Gets the pricing strategy implemented by this engine.
    /// </summary>
    PricingStrategy Strategy { get; }

    /// <summary>
    /// Calculates the price for a given set of inputs.
    /// </summary>
    /// <param name="request">The pricing request containing geometry and material information.</param>
    /// <param name="configuration">The pricing configuration to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pricing result with full breakdown.</returns>
    Task<PricingResult> CalculateAsync(
        PricingRequest request,
        PricingConfiguration configuration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for pricing calculation.
/// </summary>
public record PricingRequest
{
    /// <summary>
    /// File ID being priced.
    /// </summary>
    public required Guid FileId { get; init; }

    /// <summary>
    /// Customer ID requesting the price.
    /// </summary>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Material ID selected.
    /// </summary>
    public required Guid MaterialId { get; init; }

    /// <summary>
    /// Material code (for denormalization).
    /// </summary>
    public required string MaterialCode { get; init; }

    /// <summary>
    /// Manufacturing process ID selected.
    /// </summary>
    public required Guid ManufacturingProcessId { get; init; }

    /// <summary>
    /// Manufacturing process name (for denormalization).
    /// </summary>
    public required string ManufacturingProcessName { get; init; }

    /// <summary>
    /// Quantity of parts.
    /// </summary>
    public int Quantity { get; init; } = 1;

    /// <summary>
    /// Geometry metrics from file analysis.
    /// </summary>
    public required GeometryMetrics Geometry { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Geometry metrics from file analysis.
/// </summary>
public record GeometryMetrics
{
    /// <summary>
    /// Volume in cubic centimeters.
    /// </summary>
    public required decimal VolumeCm3 { get; init; }

    /// <summary>
    /// Support volume in cubic centimeters.
    /// </summary>
    public required decimal SupportVolumeCm3 { get; init; }

    /// <summary>
    /// Surface area in square centimeters.
    /// </summary>
    public required decimal SurfaceAreaCm2 { get; init; }

    /// <summary>
    /// Bounding box X dimension in millimeters.
    /// </summary>
    public required decimal BoundingBoxX { get; init; }

    /// <summary>
    /// Bounding box Y dimension in millimeters.
    /// </summary>
    public required decimal BoundingBoxY { get; init; }

    /// <summary>
    /// Bounding box Z dimension in millimeters.
    /// </summary>
    public required decimal BoundingBoxZ { get; init; }

    /// <summary>
    /// Whether the mesh is manifold.
    /// </summary>
    public required bool IsManifold { get; init; }

    /// <summary>
    /// Number of triangles in the mesh.
    /// </summary>
    public required int TriangleCount { get; init; }
}

/// <summary>
/// Result of pricing calculation.
/// </summary>
public record PricingResult
{
    /// <summary>
    /// Strategy used for calculation.
    /// </summary>
    public required PricingStrategy Strategy { get; init; }

    /// <summary>
    /// ML model version if ML was used.
    /// </summary>
    public string? MLModelVersion { get; init; }

    /// <summary>
    /// Material cost component.
    /// </summary>
    public required decimal MaterialCost { get; init; }

    /// <summary>
    /// Support material cost component.
    /// </summary>
    public required decimal SupportMaterialCost { get; init; }

    /// <summary>
    /// Machine time cost component.
    /// </summary>
    public required decimal MachineTimeCost { get; init; }

    /// <summary>
    /// Setup cost component.
    /// </summary>
    public required decimal SetupCost { get; init; }

    /// <summary>
    /// Complexity surcharge component.
    /// </summary>
    public required decimal ComplexitySurcharge { get; init; }

    /// <summary>
    /// Subtotal before margin.
    /// </summary>
    public required decimal SubtotalBeforeMargin { get; init; }

    /// <summary>
    /// Margin amount.
    /// </summary>
    public required decimal MarginAmount { get; init; }

    /// <summary>
    /// Unit price (per part).
    /// </summary>
    public required decimal TotalUnitPrice { get; init; }

    /// <summary>
    /// Total price (unit price × quantity).
    /// </summary>
    public required decimal TotalPrice { get; init; }

    /// <summary>
    /// Confidence level (0.0 - 1.0).
    /// </summary>
    public required decimal ConfidenceLevel { get; init; }

    /// <summary>
    /// Calculation duration.
    /// </summary>
    public required TimeSpan CalculationDuration { get; init; }
}
```

**Checklist**:
- [ ] Interface created
- [ ] PricingRequest record created
- [ ] GeometryMetrics record created
- [ ] PricingResult record created
- [ ] XML documentation on all members

### Task 3.2: Implement RuleBasedPricingEngine

**File**: `Maliev.PricingService.Api/Services/RuleBasedPricingEngine.cs`

```csharp
using System.Diagnostics;
using Maliev.PricingService.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Api.Services;

/// <summary>
/// Rule-based pricing engine using deterministic formulas.
/// </summary>
public class RuleBasedPricingEngine : IPricingEngine
{
    private readonly ILogger<RuleBasedPricingEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleBasedPricingEngine"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RuleBasedPricingEngine(ILogger<RuleBasedPricingEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public PricingStrategy Strategy => PricingStrategy.RuleBased;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(
        PricingRequest request,
        PricingConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Calculating rule-based price for file {FileId}, material {MaterialId}, process {ProcessId}",
            request.FileId,
            request.MaterialId,
            request.ManufacturingProcessId);

        // 1. Material cost
        var materialCost = request.Geometry.VolumeCm3 * config.MaterialPricePerCm3;

        // 2. Support material cost
        var supportCost = request.Geometry.SupportVolumeCm3 * config.SupportMaterialPricePerCm3;

        // 3. Machine time cost
        var printTimeHours = request.Geometry.VolumeCm3 / config.PrintSpeedCm3PerHour;
        var machineTimeCost = printTimeHours * config.MachineHourlyRate;

        // 4. Setup cost (one-time per job)
        var setupCost = config.SetupCostFlat;

        // 5. Complexity surcharge (based on surface area to volume ratio)
        var complexityRatio = request.Geometry.SurfaceAreaCm2 / request.Geometry.VolumeCm3;
        var complexitySurcharge = 0m;
        if (complexityRatio > config.ComplexityThreshold)
        {
            var baseCost = materialCost + supportCost + machineTimeCost;
            complexitySurcharge = baseCost * (config.ComplexitySurchargePercent / 100m);
        }

        // 6. Subtotal before margin
        var subtotalBeforeMargin = materialCost + supportCost + machineTimeCost + setupCost + complexitySurcharge;

        // 7. Apply margin
        var marginAmount = subtotalBeforeMargin * (config.MarginMultiplier - 1m);
        var unitPrice = subtotalBeforeMargin + marginAmount;

        // 8. Apply minimum order price
        unitPrice = Math.Max(unitPrice, config.MinimumOrderPrice);

        // 9. Calculate total price
        var totalPrice = unitPrice * request.Quantity;

        // 10. Round to 2 decimal places
        materialCost = Math.Round(materialCost, 2, MidpointRounding.AwayFromZero);
        supportCost = Math.Round(supportCost, 2, MidpointRounding.AwayFromZero);
        machineTimeCost = Math.Round(machineTimeCost, 2, MidpointRounding.AwayFromZero);
        complexitySurcharge = Math.Round(complexitySurcharge, 2, MidpointRounding.AwayFromZero);
        subtotalBeforeMargin = Math.Round(subtotalBeforeMargin, 2, MidpointRounding.AwayFromZero);
        marginAmount = Math.Round(marginAmount, 2, MidpointRounding.AwayFromZero);
        unitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        totalPrice = Math.Round(totalPrice, 2, MidpointRounding.AwayFromZero);

        stopwatch.Stop();

        _logger.LogInformation(
            "Rule-based pricing complete for file {FileId}: {TotalPrice} THB (took {Duration}ms)",
            request.FileId,
            totalPrice,
            stopwatch.ElapsedMilliseconds);

        return Task.FromResult(new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MLModelVersion = null,
            MaterialCost = materialCost,
            SupportMaterialCost = supportCost,
            MachineTimeCost = machineTimeCost,
            SetupCost = setupCost,
            ComplexitySurcharge = complexitySurcharge,
            SubtotalBeforeMargin = subtotalBeforeMargin,
            MarginAmount = marginAmount,
            TotalUnitPrice = unitPrice,
            TotalPrice = totalPrice,
            ConfidenceLevel = 0.8m, // Rule-based has 80% confidence (±20% accuracy)
            CalculationDuration = stopwatch.Elapsed
        });
    }
}
```

**Checklist**:
- [ ] Engine implemented
- [ ] All cost components calculated
- [ ] Complexity surcharge logic
- [ ] Minimum price enforcement
- [ ] Rounding to 2 decimal places
- [ ] Logging with correlation
- [ ] XML documentation

---

## Phase 4: Implement API Controllers

### Task 4.1: Create PricingController

**File**: `Maliev.PricingService.Api/Controllers/v1/PricingController.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Controllers.v1;

/// <summary>
/// Controller for pricing calculations.
/// </summary>
[ApiController]
[Route("pricing/v1")]
[Authorize]
public class PricingController : ControllerBase
{
    private readonly PricingDbContext _dbContext;
    private readonly IPricingEngine _ruleBasedEngine;
    private readonly ILogger<PricingController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingController"/> class.
    /// </summary>
    public PricingController(
        PricingDbContext dbContext,
        RuleBasedPricingEngine ruleBasedEngine,
        ILogger<PricingController> logger)
    {
        _dbContext = dbContext;
        _ruleBasedEngine = ruleBasedEngine;
        _logger = logger;
    }

    /// <summary>
    /// Calculates an instant price based on geometry and material.
    /// </summary>
    /// <param name="request">The pricing calculation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated price with full breakdown.</returns>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(CalculatePriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculatePriceResponse>> CalculatePrice(
        [FromBody] CalculatePriceRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Find active pricing configuration
        var config = await _dbContext.PricingConfigurations
            .Where(c => c.MaterialId == request.MaterialId
                && c.ManufacturingProcessId == request.ManufacturingProcessId
                && c.IsActive
                && c.EffectiveFrom <= DateTime.UtcNow
                && (c.EffectiveTo == null || c.EffectiveTo > DateTime.UtcNow))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Pricing Configuration Not Found",
                Detail = $"No active pricing configuration found for material {request.MaterialId} and process {request.ManufacturingProcessId}",
                Status = StatusCodes.Status404NotFound
            });
        }

        // 2. Calculate price
        var pricingRequest = new PricingRequest
        {
            FileId = request.FileId,
            CustomerId = request.CustomerId,
            MaterialId = request.MaterialId,
            MaterialCode = request.MaterialCode,
            ManufacturingProcessId = request.ManufacturingProcessId,
            ManufacturingProcessName = request.ManufacturingProcessName,
            Quantity = request.Quantity,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = request.VolumeCm3,
                SupportVolumeCm3 = request.SupportVolumeCm3,
                SurfaceAreaCm2 = request.SurfaceAreaCm2,
                BoundingBoxX = request.BoundingBoxX,
                BoundingBoxY = request.BoundingBoxY,
                BoundingBoxZ = request.BoundingBoxZ,
                IsManifold = request.IsManifold,
                TriangleCount = request.TriangleCount
            },
            CorrelationId = HttpContext.TraceIdentifier
        };

        var result = await _ruleBasedEngine.CalculateAsync(pricingRequest, config, cancellationToken);

        // 3. Create audit record
        var auditRecord = new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = request.FileId,
            CustomerId = request.CustomerId,
            InputVolumeCm3 = request.VolumeCm3,
            InputSupportVolumeCm3 = request.SupportVolumeCm3,
            InputSurfaceAreaCm2 = request.SurfaceAreaCm2,
            InputBoundingBoxX = request.BoundingBoxX,
            InputBoundingBoxY = request.BoundingBoxY,
            InputBoundingBoxZ = request.BoundingBoxZ,
            InputIsManifold = request.IsManifold,
            InputTriangleCount = request.TriangleCount,
            MaterialId = request.MaterialId,
            MaterialCode = request.MaterialCode,
            ManufacturingProcessId = request.ManufacturingProcessId,
            ManufacturingProcessName = request.ManufacturingProcessName,
            Quantity = request.Quantity,
            PricingConfigurationId = config.Id,
            ConfigMaterialPricePerCm3 = config.MaterialPricePerCm3,
            ConfigSupportPricePerCm3 = config.SupportMaterialPricePerCm3,
            ConfigMachineHourlyRate = config.MachineHourlyRate,
            ConfigMarginMultiplier = config.MarginMultiplier,
            Strategy = result.Strategy,
            MLModelVersion = result.MLModelVersion,
            MaterialCost = result.MaterialCost,
            SupportMaterialCost = result.SupportMaterialCost,
            MachineTimeCost = result.MachineTimeCost,
            SetupCost = result.SetupCost,
            ComplexitySurcharge = result.ComplexitySurcharge,
            SubtotalBeforeMargin = result.SubtotalBeforeMargin,
            MarginAmount = result.MarginAmount,
            TotalUnitPrice = result.TotalUnitPrice,
            TotalPrice = result.TotalPrice,
            ConfidenceLevel = result.ConfidenceLevel,
            CurrencyCode = "THB",
            ValidFrom = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(7),
            CalculatedAt = DateTime.UtcNow,
            CalculatedBySystem = "PricingService",
            CorrelationId = HttpContext.TraceIdentifier,
            CalculationDuration = result.CalculationDuration
        };

        _dbContext.PricingAuditRecords.Add(auditRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created pricing audit record {AuditRecordId} for file {FileId}",
            auditRecord.Id,
            request.FileId);

        // 4. Return response
        return Ok(new CalculatePriceResponse
        {
            PricingAuditId = auditRecord.Id,
            TotalPrice = result.TotalPrice,
            UnitPrice = result.TotalUnitPrice,
            Currency = "THB",
            ConfidenceLevel = result.ConfidenceLevel,
            ValidUntil = auditRecord.ValidUntil,
            Breakdown = new PriceBreakdown
            {
                MaterialCost = result.MaterialCost,
                SupportMaterialCost = result.SupportMaterialCost,
                MachineTimeCost = result.MachineTimeCost,
                SetupCost = result.SetupCost,
                ComplexitySurcharge = result.ComplexitySurcharge,
                SubtotalBeforeMargin = result.SubtotalBeforeMargin,
                MarginAmount = result.MarginAmount
            },
            Strategy = result.Strategy.ToString(),
            CalculationDurationMs = (int)result.CalculationDuration.TotalMilliseconds
        });
    }

    /// <summary>
    /// Gets a pricing audit record by ID.
    /// </summary>
    /// <param name="id">The audit record ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audit record.</returns>
    [HttpGet("audit/{id:guid}")]
    [ProducesResponseType(typeof(PricingAuditRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingAuditRecord>> GetAuditRecord(
        Guid id,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.PricingAuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (record == null)
        {
            return NotFound();
        }

        return Ok(record);
    }

    /// <summary>
    /// Gets all pricing audit records for a file.
    /// </summary>
    /// <param name="fileId">The file ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of audit records.</returns>
    [HttpGet("audit/file/{fileId:guid}")]
    [ProducesResponseType(typeof(List<PricingAuditRecord>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PricingAuditRecord>>> GetAuditRecordsByFile(
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.PricingAuditRecords
            .AsNoTracking()
            .Where(r => r.FileId == fileId)
            .OrderByDescending(r => r.CalculatedAt)
            .ToListAsync(cancellationToken);

        return Ok(records);
    }
}

/// <summary>
/// Request to calculate a price.
/// </summary>
public record CalculatePriceRequest
{
    /// <summary>File ID.</summary>
    [Required]
    public Guid FileId { get; init; }

    /// <summary>Customer ID.</summary>
    [Required]
    public Guid CustomerId { get; init; }

    /// <summary>Material ID.</summary>
    [Required]
    public Guid MaterialId { get; init; }

    /// <summary>Material code.</summary>
    [Required]
    [StringLength(50)]
    public string MaterialCode { get; init; } = string.Empty;

    /// <summary>Manufacturing process ID.</summary>
    [Required]
    public Guid ManufacturingProcessId { get; init; }

    /// <summary>Manufacturing process name.</summary>
    [Required]
    [StringLength(100)]
    public string ManufacturingProcessName { get; init; } = string.Empty;

    /// <summary>Quantity.</summary>
    [Range(1, 1000000)]
    public int Quantity { get; init; } = 1;

    /// <summary>Volume in cm³.</summary>
    [Required]
    [Range(0.001, 1000000)]
    public decimal VolumeCm3 { get; init; }

    /// <summary>Support volume in cm³.</summary>
    [Range(0, 1000000)]
    public decimal SupportVolumeCm3 { get; init; }

    /// <summary>Surface area in cm².</summary>
    [Required]
    [Range(0.01, 10000000)]
    public decimal SurfaceAreaCm2 { get; init; }

    /// <summary>Bounding box X in mm.</summary>
    [Required]
    [Range(0.1, 10000)]
    public decimal BoundingBoxX { get; init; }

    /// <summary>Bounding box Y in mm.</summary>
    [Required]
    [Range(0.1, 10000)]
    public decimal BoundingBoxY { get; init; }

    /// <summary>Bounding box Z in mm.</summary>
    [Required]
    [Range(0.1, 10000)]
    public decimal BoundingBoxZ { get; init; }

    /// <summary>Is manifold.</summary>
    public bool IsManifold { get; init; }

    /// <summary>Triangle count.</summary>
    [Range(1, int.MaxValue)]
    public int TriangleCount { get; init; }
}

/// <summary>
/// Response from price calculation.
/// </summary>
public record CalculatePriceResponse
{
    /// <summary>Pricing audit record ID for traceability.</summary>
    public Guid PricingAuditId { get; init; }

    /// <summary>Total price.</summary>
    public decimal TotalPrice { get; init; }

    /// <summary>Unit price (per part).</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Currency code.</summary>
    public string Currency { get; init; } = "THB";

    /// <summary>Confidence level (0.0 - 1.0).</summary>
    public decimal ConfidenceLevel { get; init; }

    /// <summary>Price valid until.</summary>
    public DateTime ValidUntil { get; init; }

    /// <summary>Price breakdown.</summary>
    public PriceBreakdown Breakdown { get; init; } = new();

    /// <summary>Pricing strategy used.</summary>
    public string Strategy { get; init; } = string.Empty;

    /// <summary>Calculation duration in milliseconds.</summary>
    public int CalculationDurationMs { get; init; }
}

/// <summary>
/// Price breakdown components.
/// </summary>
public record PriceBreakdown
{
    /// <summary>Material cost.</summary>
    public decimal MaterialCost { get; init; }

    /// <summary>Support material cost.</summary>
    public decimal SupportMaterialCost { get; init; }

    /// <summary>Machine time cost.</summary>
    public decimal MachineTimeCost { get; init; }

    /// <summary>Setup cost.</summary>
    public decimal SetupCost { get; init; }

    /// <summary>Complexity surcharge.</summary>
    public decimal ComplexitySurcharge { get; init; }

    /// <summary>Subtotal before margin.</summary>
    public decimal SubtotalBeforeMargin { get; init; }

    /// <summary>Margin amount.</summary>
    public decimal MarginAmount { get; init; }
}
```

**Checklist**:
- [ ] Controller created
- [ ] Calculate endpoint implemented
- [ ] Audit record created for every calculation
- [ ] Get audit record endpoints implemented
- [ ] Request/response DTOs created
- [ ] XML documentation on all members
- [ ] Proper authorization attributes

---

## Phase 5: Implement Event Consumers

### Task 5.1: Create FileAnalyzedEventConsumer

**File**: `Maliev.PricingService.Api/Consumers/FileAnalyzedEventConsumer.cs`

```csharp
using Maliev.MessagingContracts.Contracts;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Consumers;

/// <summary>
/// Consumes FileAnalyzedEvent from GeometryService and calculates pricing.
/// </summary>
public class FileAnalyzedEventConsumer : IConsumer<FileAnalyzedEvent>
{
    private readonly PricingDbContext _dbContext;
    private readonly RuleBasedPricingEngine _pricingEngine;
    private readonly IMaterialServiceClient _materialClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FileAnalyzedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAnalyzedEventConsumer"/> class.
    /// </summary>
    public FileAnalyzedEventConsumer(
        PricingDbContext dbContext,
        RuleBasedPricingEngine pricingEngine,
        IMaterialServiceClient materialClient,
        IPublishEndpoint publishEndpoint,
        ILogger<FileAnalyzedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _pricingEngine = pricingEngine;
        _materialClient = materialClient;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<FileAnalyzedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received FileAnalyzedEvent for file {FileId}",
            message.FileId);

        // TODO: Implement logic to:
        // 1. Get material/process selection from request context or default
        // 2. Calculate price using pricing engine
        // 3. Create audit record
        // 4. Publish PriceCalculatedEvent

        // For now, this is a placeholder for event-driven pricing
        // The actual implementation depends on how material selection is passed
    }
}
```

**Checklist**:
- [ ] Consumer created
- [ ] Dependencies injected
- [ ] Basic structure implemented
- [ ] TODO comments for implementation details

---

## Phase 6: Update MessagingContracts

### Task 6.1: Add Pricing Event Schemas

**File**: `Maliev.MessagingContracts/contracts/schemas/pricing/pricing-events.json`

Create AsyncAPI schema for pricing events.

### Task 6.2: Add C# Event Classes

**File**: `Maliev.MessagingContracts/Contracts/Pricing/PriceCalculatedEvent.cs`

```csharp
namespace Maliev.MessagingContracts.Contracts.Pricing;

/// <summary>
/// Event published when a price has been calculated.
/// </summary>
public record PriceCalculatedEvent
{
    /// <summary>
    /// Pricing audit record ID for traceability.
    /// </summary>
    public required Guid PricingAuditId { get; init; }

    /// <summary>
    /// File ID that was priced.
    /// </summary>
    public required Guid FileId { get; init; }

    /// <summary>
    /// Customer ID requesting the quote.
    /// </summary>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Material ID used for pricing.
    /// </summary>
    public required Guid MaterialId { get; init; }

    /// <summary>
    /// Manufacturing process ID used for pricing.
    /// </summary>
    public required Guid ProcessId { get; init; }

    /// <summary>
    /// Quantity of parts.
    /// </summary>
    public required int Quantity { get; init; }

    /// <summary>
    /// Pricing strategy used.
    /// </summary>
    public required string Strategy { get; init; }

    /// <summary>
    /// Price breakdown.
    /// </summary>
    public required PriceBreakdownContract Breakdown { get; init; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Confidence level (0.0 - 1.0).
    /// </summary>
    public required decimal ConfidenceLevel { get; init; }

    /// <summary>
    /// Price valid until.
    /// </summary>
    public required DateTime ValidUntil { get; init; }

    /// <summary>
    /// When the price was calculated.
    /// </summary>
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>
/// Price breakdown in event contract.
/// </summary>
public record PriceBreakdownContract
{
    /// <summary>Material cost.</summary>
    public required decimal MaterialCost { get; init; }

    /// <summary>Support material cost.</summary>
    public required decimal SupportCost { get; init; }

    /// <summary>Machine time cost.</summary>
    public required decimal MachineTimeCost { get; init; }

    /// <summary>Setup cost.</summary>
    public required decimal SetupCost { get; init; }

    /// <summary>Complexity surcharge.</summary>
    public required decimal ComplexitySurcharge { get; init; }

    /// <summary>Margin amount.</summary>
    public required decimal MarginAmount { get; init; }

    /// <summary>Total price.</summary>
    public required decimal TotalPrice { get; init; }
}
```

**Checklist**:
- [ ] JSON schema created
- [ ] C# event classes created
- [ ] XML documentation on all members

---

## Phase 7: Update QuotationService Integration

### Task 7.1: Add PriceCalculatedEventConsumer to QuotationService

**File**: `Maliev.QuotationService.Api/Consumers/PriceCalculatedEventConsumer.cs`

Create consumer that creates draft quotations from pricing events.

**Checklist**:
- [ ] Consumer created in QuotationService
- [ ] Program.cs updated to register consumer
- [ ] Draft quotation creation logic implemented

---

## Phase 8: Historical Data Migration

### Task 8.1: Create Migration Script

Create SQL script to migrate historical pricing data from legacy PostgreSQL database.

### Task 8.2: Validate Migrated Data

Create validation queries to ensure data integrity.

**Checklist**:
- [ ] Migration script created
- [ ] Test run on development database
- [ ] Data validation queries run
- [ ] Outliers flagged for review

---

## Phase 9: Testing

### Task 9.1: Create Test Factory

**File**: `Maliev.PricingService.Tests/TestFixtures/PricingServiceTestFactory.cs`

### Task 9.2: Create Integration Tests

**File**: `Maliev.PricingService.Tests/Integration/PricingControllerTests.cs`

**Checklist**:
- [ ] Test factory created with Testcontainers
- [ ] Controller integration tests created
- [ ] Pricing engine unit tests created
- [ ] Event consumer tests created
- [ ] All tests passing

---

## Phase 10: GitOps & Deployment

### Task 10.1: Create Kubernetes Manifests

**Location**: `maliev-gitops/3-apps/maliev-pricing-service/base/`

### Task 10.2: Create ArgoCD Application

**File**: `maliev-gitops/argocd/maliev-pricing-service.yaml`

**Checklist**:
- [ ] Deployment manifest created
- [ ] Service manifest created
- [ ] HPA manifest created
- [ ] ExternalSecret manifest created
- [ ] ArgoCD Application created
- [ ] CI/CD workflows created

---

## Verification Checklist

### Functional Requirements
- [ ] POST /pricing/v1/calculate returns correct price breakdown
- [ ] GET /pricing/v1/audit/{id} returns audit record
- [ ] GET /pricing/v1/audit/file/{fileId} returns all audit records for file
- [ ] Pricing configurations can be CRUD'd
- [ ] Historical data imported successfully

### Non-Functional Requirements
- [ ] Calculation completes in < 100ms
- [ ] End-to-end (file upload to instant quote) completes in < 3 seconds
- [ ] Audit records are immutable (no updates)
- [ ] All calculations logged with correlation ID
- [ ] 7-year retention policy implemented

### Integration Tests
- [ ] MaterialService integration works
- [ ] CurrencyService integration works
- [ ] Event publishing works
- [ ] QuotationService consumes events correctly

### Security
- [ ] IAM permissions registered
- [ ] Authorization enforced on all endpoints
- [ ] No secrets in code
- [ ] Audit trail complete

### Deployment
- [ ] Service deploys to development environment
- [ ] Health checks pass
- [ ] Metrics exposed
- [ ] Logs structured correctly
