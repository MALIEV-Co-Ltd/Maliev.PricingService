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
14. [Phase 11: Aspire Orchestration](#phase-11-aspire-orchestration)
15. [Verification Checklist](#verification-checklist)

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
├── Maliev.PricingService.slnx
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

### Task 1.4: Create Program.cs

> **Status**: COMPLETED - Standardized to follow MALIEV pattern

**File**: `Maliev.PricingService.Api/Program.cs`

The Program.cs follows the standardized MALIEV pattern with bootstrap logging, try-catch-finally error handling, and all required infrastructure components.

**Key Components**:

1. **Bootstrap Logger** - Console logger initialized before host starts for startup error visibility
2. **Try-Catch-Finally Block** - Proper error handling with forced console flush for Aspire
3. **Google Secret Manager** - `AddGoogleSecretManagerVolume()` for production secrets
4. **Service Defaults** - OpenTelemetry, health checks, resilience patterns
5. **Standard Middleware** - Request logging, correlation IDs, security headers
6. **Service Meters** - OpenTelemetry business metrics (`pricing-meter`)
7. **PostgreSQL** - `AddPostgresDbContext<PricingDbContext>(connectionName: "PricingDbContext")`
8. **Redis Cache** - `AddRedisDistributedCache(instanceName: "pricing:")`
9. **MassTransit** - `AddMassTransitWithRabbitMq()` with FileAnalyzedEventConsumer and OrderCompletedEventConsumer
10. **CORS** - `AddDefaultCors()` from config
11. **API Versioning** - `AddDefaultApiVersioning()` with URL segment reader
12. **JWT Authentication** - `AddJwtAuthentication()` with RSA/HMAC support
13. **OpenAPI** - `AddStandardOpenApi()` with Scalar documentation (dev/staging only)
14. **Service Clients** - MaterialService and CurrencyService with `AddServiceClient()`
15. **IAM Integration** - `AddIAMServiceClient("pricing")` and `AddIAMRegistration<PricingIAMRegistrationService>()`
16. **Pricing Services** - RuleBasedPricingEngine, MLPricingEngine, PricingOrchestrator
17. **Database Migrations** - `MigrateDatabaseAsync<PricingDbContext>()` on startup
18. **Middleware Pipeline** - UseStandardMiddleware, Authentication, Authorization
19. **Endpoint Mapping** - MapDefaultEndpoints, MapApiDocumentation, MapControllers
20. **LoggerMessage Definitions** - Partial Program class with StartingHost, ServiceStarted, HostTerminated

**Additional Files Created**:

**`Services/PricingPermissions.cs`** - Permission constants and predefined roles:
- Permissions: calculations.create, audit.read, configurations.*, models.*
- Roles: Admin, Analyst, Viewer, Calculator

**`Services/PricingIAMRegistrationService.cs`** - Auto-registers permissions and roles with IAM on startup

**Checklist**:
- [x] Program.cs created with MALIEV pattern
- [x] Bootstrap logger with try-catch-finally
- [x] Google Secret Manager integration
- [x] ServiceDefaults configured (OpenTelemetry, health checks)
- [x] Standard middleware with request logging
- [x] Service meters for business metrics
- [x] JWT authentication configured
- [x] PostgreSQL with connection name
- [x] Redis with instance name prefix
- [x] MassTransit with consumers configured
- [x] API versioning configured
- [x] CORS configured
- [x] OpenAPI with Scalar documentation
- [x] IAM client and registration configured
- [x] MaterialService and CurrencyService clients with AddServiceClient
- [x] Pricing engines registered (RuleBasedPricingEngine, MLPricingEngine, PricingOrchestrator)
- [x] Database migrations on startup
- [x] Authentication and authorization middleware
- [x] Endpoints mapped (DefaultEndpoints, ApiDocumentation, Controllers)
- [x] LoggerMessage definitions in partial Program class
- [x] PricingPermissions and predefined roles created
- [x] PricingIAMRegistrationService created

### Task 1.5: Create appsettings.json

**File**: `Maliev.PricingService.Api/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.AspNetCore.Watch.BrowserRefresh": "None",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.AspNetCore.Watch": "Warning",
      "System": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Services": {
    "MaterialService": {
      "BaseUrl": "http://maliev-materialservice-api"
    },
    "CurrencyService": {
      "BaseUrl": "http://maliev-currencyservice-api"
    },
    "IAMService": {
      "BaseUrl": "http://maliev-iamservice-api"
    }
  }
}
```

**Note**: Configuration follows MALIEV platform standards:
- Standard logging levels suppress verbose ASP.NET Core and EF Core logs
- `Services` section uses `{ServiceName}:BaseUrl` pattern (enforced)
- All secrets (DB connections, JWT, RabbitMQ) injected via environment variables
- Aspire provides infrastructure configuration for local development

**File**: `Maliev.PricingService.Api/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Note**: Development overrides enable verbose logging:
- `Microsoft.AspNetCore`: Information level shows detailed request/response logs
- `Microsoft.EntityFrameworkCore.Database.Command`: Shows SQL queries
- All infrastructure (DB, Redis, RabbitMQ) configured via Aspire - no hardcoded values

**Checklist**:
- [x] appsettings.json created with standard MALIEV logging configuration
- [x] appsettings.Development.json created with verbose logging
- [x] Services URLs configured (MaterialService, CurrencyService, IAMService)
- [x] Follows enforced `Services:{ServiceName}:BaseUrl` pattern
- [x] No hardcoded secrets or connection strings

### Task 1.6: Create Dockerfile

**File**: `Maliev.PricingService.Api/Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files
COPY ["Maliev.PricingService.Api/Maliev.PricingService.Api.csproj", "Maliev.PricingService.Api/"]
COPY ["Maliev.PricingService.Data/Maliev.PricingService.Data.csproj", "Maliev.PricingService.Data/"]
COPY ["nuget.config", "."]

# Restore dependencies
ARG GITHUB_TOKEN
RUN dotnet restore "Maliev.PricingService.Api/Maliev.PricingService.Api.csproj"

# Copy everything and build
COPY . .
WORKDIR "/src/Maliev.PricingService.Api"
RUN dotnet build "Maliev.PricingService.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Maliev.PricingService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Create non-root user for security
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/pricing/liveness || exit 1

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Maliev.PricingService.Api.dll"]
```

**Checklist**:
- [ ] Dockerfile created
- [ ] Multi-stage build configured
- [ ] Non-root user configured
- [ ] Health check configured
- [ ] Port 8080 exposed

### Task 1.7: Create Tests Project Configuration

**File**: `Maliev.PricingService.Tests/Maliev.PricingService.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.3.0" />
    <PackageReference Include="Testcontainers.Redis" Version="4.3.0" />
    <PackageReference Include="Testcontainers.RabbitMq" Version="4.3.0" />
    <PackageReference Include="MassTransit.Testing" Version="8.4.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Maliev.PricingService.Api\Maliev.PricingService.Api.csproj" />
    <ProjectReference Include="..\Maliev.PricingService.Data\Maliev.PricingService.Data.csproj" />
  </ItemGroup>
</Project>
```

**Checklist**:
- [ ] Tests project .csproj created
- [ ] Testcontainers packages added
- [ ] MassTransit.Testing package added
- [ ] xUnit packages configured
- [ ] TreatWarningsAsErrors enabled

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

**File**: `Maliev.PricingService.Data/Configurations/PricingAuditRecordEntityConfiguration.cs`

```csharp
using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PricingService.Data.Configurations;

/// <summary>
/// Entity configuration for PricingAuditRecord.
/// </summary>
public class PricingAuditRecordEntityConfiguration : IEntityTypeConfiguration<PricingAuditRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PricingAuditRecord> builder)
    {
        builder.ToTable("pricing_audit_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.QuotationId)
            .HasColumnName("quotation_id");

        builder.Property(x => x.FileId)
            .HasColumnName("file_id")
            .IsRequired();

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        // Geometry inputs
        builder.Property(x => x.InputVolumeCm3)
            .HasColumnName("input_volume_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.InputSupportVolumeCm3)
            .HasColumnName("input_support_volume_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.InputSurfaceAreaCm2)
            .HasColumnName("input_surface_area_cm2")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.InputBoundingBoxX)
            .HasColumnName("input_bounding_box_x")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.InputBoundingBoxY)
            .HasColumnName("input_bounding_box_y")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.InputBoundingBoxZ)
            .HasColumnName("input_bounding_box_z")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.InputIsManifold)
            .HasColumnName("input_is_manifold")
            .IsRequired();

        builder.Property(x => x.InputTriangleCount)
            .HasColumnName("input_triangle_count")
            .IsRequired();

        // Material/Process
        builder.Property(x => x.MaterialId)
            .HasColumnName("material_id")
            .IsRequired();

        builder.Property(x => x.MaterialCode)
            .HasColumnName("material_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ManufacturingProcessId)
            .HasColumnName("manufacturing_process_id")
            .IsRequired();

        builder.Property(x => x.ManufacturingProcessName)
            .HasColumnName("manufacturing_process_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasDefaultValue(1);

        // Config snapshot
        builder.Property(x => x.PricingConfigurationId)
            .HasColumnName("pricing_configuration_id")
            .IsRequired();

        builder.Property(x => x.ConfigMaterialPricePerCm3)
            .HasColumnName("config_material_price_per_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.ConfigSupportPricePerCm3)
            .HasColumnName("config_support_price_per_cm3")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.ConfigMachineHourlyRate)
            .HasColumnName("config_machine_hourly_rate")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ConfigMarginMultiplier)
            .HasColumnName("config_margin_multiplier")
            .HasPrecision(5, 2)
            .IsRequired();

        // Strategy
        builder.Property(x => x.Strategy)
            .HasColumnName("strategy")
            .IsRequired();

        builder.Property(x => x.MLModelVersion)
            .HasColumnName("ml_model_version")
            .HasMaxLength(50);

        // Price breakdown
        builder.Property(x => x.MaterialCost)
            .HasColumnName("material_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.SupportMaterialCost)
            .HasColumnName("support_material_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MachineTimeCost)
            .HasColumnName("machine_time_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.SetupCost)
            .HasColumnName("setup_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ComplexitySurcharge)
            .HasColumnName("complexity_surcharge")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.SubtotalBeforeMargin)
            .HasColumnName("subtotal_before_margin")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MarginAmount)
            .HasColumnName("margin_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalUnitPrice)
            .HasColumnName("total_unit_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalPrice)
            .HasColumnName("total_price")
            .HasPrecision(18, 2)
            .IsRequired();

        // Metadata
        builder.Property(x => x.ConfidenceLevel)
            .HasColumnName("confidence_level")
            .HasPrecision(3, 2)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasDefaultValue("THB");

        builder.Property(x => x.ValidFrom)
            .HasColumnName("valid_from")
            .IsRequired();

        builder.Property(x => x.ValidUntil)
            .HasColumnName("valid_until")
            .IsRequired();

        builder.Property(x => x.CalculatedAt)
            .HasColumnName("calculated_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(x => x.CalculatedBySystem)
            .HasColumnName("calculated_by_system")
            .HasMaxLength(50)
            .HasDefaultValue("PricingService");

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

        builder.Property(x => x.CalculationDuration)
            .HasColumnName("calculation_duration")
            .IsRequired();

        // Indexes for quick lookups
        builder.HasIndex(x => x.FileId)
            .HasDatabaseName("idx_pricing_audit_file");

        builder.HasIndex(x => x.CustomerId)
            .HasDatabaseName("idx_pricing_audit_customer");

        builder.HasIndex(x => x.QuotationId)
            .HasDatabaseName("idx_pricing_audit_quotation");

        builder.HasIndex(x => x.CalculatedAt)
            .HasDatabaseName("idx_pricing_audit_calculated_at");

        // Navigation
        builder.HasOne(x => x.PricingConfiguration)
            .WithMany()
            .HasForeignKey(x => x.PricingConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TrainingData)
            .WithOne(x => x.PricingAuditRecord)
            .HasForeignKey<PricingTrainingData>(x => x.PricingAuditRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**File**: `Maliev.PricingService.Data/Configurations/PricingModelEntityConfiguration.cs`

```csharp
using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PricingService.Data.Configurations;

/// <summary>
/// Entity configuration for PricingModel.
/// </summary>
public class PricingModelEntityConfiguration : IEntityTypeConfiguration<PricingModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PricingModel> builder)
    {
        builder.ToTable("pricing_models");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ModelType)
            .HasColumnName("model_type")
            .IsRequired();

        // Training metadata
        builder.Property(x => x.TrainingDataCount)
            .HasColumnName("training_data_count")
            .IsRequired();

        builder.Property(x => x.TrainingStartedAt)
            .HasColumnName("training_started_at")
            .IsRequired();

        builder.Property(x => x.TrainingCompletedAt)
            .HasColumnName("training_completed_at")
            .IsRequired();

        builder.Property(x => x.TrainingDuration)
            .HasColumnName("training_duration")
            .IsRequired();

        // Performance metrics
        builder.Property(x => x.MeanAbsoluteError)
            .HasColumnName("mean_absolute_error")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.MeanAbsolutePercentageError)
            .HasColumnName("mean_absolute_percentage_error")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.RSquared)
            .HasColumnName("r_squared")
            .HasPrecision(5, 4)
            .IsRequired();

        // Deployment status
        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(false);

        builder.Property(x => x.DeployedAt)
            .HasColumnName("deployed_at");

        builder.Property(x => x.RetiredAt)
            .HasColumnName("retired_at");

        // Storage
        builder.Property(x => x.ModelFilePath)
            .HasColumnName("model_file_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_pricing_models_active");

        builder.HasIndex(x => new { x.Name, x.Version })
            .IsUnique()
            .HasDatabaseName("idx_pricing_models_name_version");
    }
}
```

**File**: `Maliev.PricingService.Data/Configurations/PricingTrainingDataEntityConfiguration.cs`

```csharp
using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PricingService.Data.Configurations;

/// <summary>
/// Entity configuration for PricingTrainingData.
/// </summary>
public class PricingTrainingDataEntityConfiguration : IEntityTypeConfiguration<PricingTrainingData>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PricingTrainingData> builder)
    {
        builder.ToTable("pricing_training_data");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PricingAuditRecordId)
            .HasColumnName("pricing_audit_record_id")
            .IsRequired();

        builder.Property(x => x.OrderId)
            .HasColumnName("order_id");

        // Outcome tracking
        builder.Property(x => x.CustomerAccepted)
            .HasColumnName("customer_accepted")
            .HasDefaultValue(false);

        builder.Property(x => x.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(x => x.JobCompleted)
            .HasColumnName("job_completed")
            .HasDefaultValue(false);

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(x => x.JobSucceeded)
            .HasColumnName("job_succeeded")
            .HasDefaultValue(false);

        // Actual costs
        builder.Property(x => x.ActualMaterialUsedCm3)
            .HasColumnName("actual_material_used_cm3")
            .HasPrecision(18, 6);

        builder.Property(x => x.ActualPrintTimeHours)
            .HasColumnName("actual_print_time_hours")
            .HasPrecision(10, 2);

        builder.Property(x => x.ActualLaborHours)
            .HasColumnName("actual_labor_hours")
            .HasPrecision(10, 2);

        builder.Property(x => x.ActualTotalCost)
            .HasColumnName("actual_total_cost")
            .HasPrecision(18, 2);

        // Profit analysis
        builder.Property(x => x.ActualProfitMargin)
            .HasColumnName("actual_profit_margin")
            .HasPrecision(5, 4);

        // Training status
        builder.Property(x => x.UsedForTraining)
            .HasColumnName("used_for_training")
            .HasDefaultValue(false);

        builder.Property(x => x.TrainedModelId)
            .HasColumnName("trained_model_id");

        // Indexes
        builder.HasIndex(x => x.PricingAuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_training_data_audit_record");

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("idx_training_data_order");

        builder.HasIndex(x => x.UsedForTraining)
            .HasDatabaseName("idx_training_data_used");

        // Navigation
        builder.HasOne(x => x.TrainedModel)
            .WithMany()
            .HasForeignKey(x => x.TrainedModelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

**Checklist**:
- [ ] PricingConfigurationEntityConfiguration created
- [ ] PricingAuditRecordEntityConfiguration created
- [ ] PricingModelEntityConfiguration created
- [ ] PricingTrainingDataEntityConfiguration created
- [ ] All snake_case column names
- [ ] Proper indexes defined
- [ ] RowVersion configured
- [ ] Navigation properties configured

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

### Task 5.1: Create Service Client Interfaces and Implementations

**File**: `Maliev.PricingService.Api/Clients/IMaterialServiceClient.cs`

```csharp
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
```

**File**: `Maliev.PricingService.Api/Clients/MaterialServiceClient.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// HTTP client for MaterialService.
/// </summary>
public class MaterialServiceClient : IMaterialServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MaterialServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialServiceClient"/> class.
    /// </summary>
    public MaterialServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MaterialServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MaterialDto?> GetMaterialAsync(Guid materialId, CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            return await _httpClient.GetFromJsonAsync<MaterialDto>(
                $"/material/v1/materials/{materialId}",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get material {MaterialId}", materialId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ManufacturingProcessDto?> GetProcessAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            return await _httpClient.GetFromJsonAsync<ManufacturingProcessDto>(
                $"/material/v1/processes/{processId}",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get process {ProcessId}", processId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MaterialDto?> GetDefaultMaterialAsync(string processType, CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            return await _httpClient.GetFromJsonAsync<MaterialDto>(
                $"/material/v1/materials/default?processType={processType}",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get default material for process type {ProcessType}", processType);
            return null;
        }
    }

    private void ForwardAuthorizationHeader()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }
    }
}
```

**File**: `Maliev.PricingService.Api/Clients/ICurrencyServiceClient.cs`

```csharp
namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// Client for communicating with CurrencyService.
/// </summary>
public interface ICurrencyServiceClient
{
    /// <summary>
    /// Converts an amount from one currency to another.
    /// </summary>
    /// <param name="amount">The amount to convert.</param>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The converted amount.</returns>
    Task<decimal> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the exchange rate between two currencies.
    /// </summary>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange rate.</returns>
    Task<decimal> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);
}
```

**File**: `Maliev.PricingService.Api/Clients/CurrencyServiceClient.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// HTTP client for CurrencyService.
/// </summary>
public class CurrencyServiceClient : ICurrencyServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrencyServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyServiceClient"/> class.
    /// </summary>
    public CurrencyServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrencyServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        if (fromCurrency == toCurrency)
        {
            return amount;
        }

        var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, cancellationToken);
        return amount * rate;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        if (fromCurrency == toCurrency)
        {
            return 1m;
        }

        ForwardAuthorizationHeader();

        try
        {
            var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(
                $"/currency/v1/rates?from={fromCurrency}&to={toCurrency}",
                cancellationToken);

            return response?.Rate ?? 1m;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get exchange rate from {From} to {To}", fromCurrency, toCurrency);
            return 1m; // Default to 1:1 if service unavailable
        }
    }

    private void ForwardAuthorizationHeader()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }
    }

    private record ExchangeRateResponse(decimal Rate);
}
```

**Checklist**:
- [ ] IMaterialServiceClient interface created
- [ ] MaterialServiceClient implementation created
- [ ] ICurrencyServiceClient interface created
- [ ] CurrencyServiceClient implementation created
- [ ] Authorization forwarding implemented
- [ ] XML documentation on all members

### Task 5.2: Create FileAnalyzedEventConsumer

**File**: `Maliev.PricingService.Api/Consumers/FileAnalyzedEventConsumer.cs`

```csharp
using Maliev.MessagingContracts.Contracts;
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Consumers;

/// <summary>
/// Consumes FileAnalyzedEvent from GeometryService and calculates pricing.
/// This consumer triggers automatic price calculation when a 3D file is analyzed.
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
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Received FileAnalyzedEvent for file {FileId}, customer {CustomerId}",
            message.FileId,
            message.CustomerId);

        try
        {
            // 1. Get material and process from the event or use defaults
            var materialId = message.MaterialId;
            var processId = message.ManufacturingProcessId;

            // If no material/process specified, get defaults based on file type
            MaterialDto? material = null;
            ManufacturingProcessDto? process = null;

            if (materialId.HasValue)
            {
                material = await _materialClient.GetMaterialAsync(materialId.Value, cancellationToken);
            }
            else
            {
                // Get default material for FDM (most common)
                material = await _materialClient.GetDefaultMaterialAsync("FDM", cancellationToken);
            }

            if (processId.HasValue)
            {
                process = await _materialClient.GetProcessAsync(processId.Value, cancellationToken);
            }

            if (material == null || process == null)
            {
                _logger.LogWarning(
                    "Could not find material or process for file {FileId}. Skipping pricing.",
                    message.FileId);
                return;
            }

            // 2. Find active pricing configuration
            var config = await _dbContext.PricingConfigurations
                .Where(c => c.MaterialId == material.Id
                    && c.ManufacturingProcessId == process.Id
                    && c.IsActive
                    && c.EffectiveFrom <= DateTime.UtcNow
                    && (c.EffectiveTo == null || c.EffectiveTo > DateTime.UtcNow))
                .OrderByDescending(c => c.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                _logger.LogWarning(
                    "No active pricing configuration for material {MaterialId} and process {ProcessId}",
                    material.Id,
                    process.Id);
                return;
            }

            // 3. Build pricing request from geometry metrics
            var pricingRequest = new PricingRequest
            {
                FileId = message.FileId,
                CustomerId = message.CustomerId,
                MaterialId = material.Id,
                MaterialCode = material.Code,
                ManufacturingProcessId = process.Id,
                ManufacturingProcessName = process.Name,
                Quantity = message.Quantity ?? 1,
                Geometry = new GeometryMetrics
                {
                    VolumeCm3 = message.VolumeCm3,
                    SupportVolumeCm3 = message.SupportVolumeCm3 ?? 0,
                    SurfaceAreaCm2 = message.SurfaceAreaCm2,
                    BoundingBoxX = message.BoundingBoxX,
                    BoundingBoxY = message.BoundingBoxY,
                    BoundingBoxZ = message.BoundingBoxZ,
                    IsManifold = message.IsManifold,
                    TriangleCount = message.TriangleCount
                },
                CorrelationId = context.CorrelationId?.ToString()
            };

            // 4. Calculate price
            var result = await _pricingEngine.CalculateAsync(pricingRequest, config, cancellationToken);

            // 5. Create audit record
            var auditRecord = new PricingAuditRecord
            {
                Id = Guid.NewGuid(),
                FileId = message.FileId,
                CustomerId = message.CustomerId,
                InputVolumeCm3 = message.VolumeCm3,
                InputSupportVolumeCm3 = message.SupportVolumeCm3 ?? 0,
                InputSurfaceAreaCm2 = message.SurfaceAreaCm2,
                InputBoundingBoxX = message.BoundingBoxX,
                InputBoundingBoxY = message.BoundingBoxY,
                InputBoundingBoxZ = message.BoundingBoxZ,
                InputIsManifold = message.IsManifold,
                InputTriangleCount = message.TriangleCount,
                MaterialId = material.Id,
                MaterialCode = material.Code,
                ManufacturingProcessId = process.Id,
                ManufacturingProcessName = process.Name,
                Quantity = message.Quantity ?? 1,
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
                CorrelationId = context.CorrelationId?.ToString(),
                CalculationDuration = result.CalculationDuration
            };

            _dbContext.PricingAuditRecords.Add(auditRecord);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created pricing audit record {AuditRecordId} for file {FileId}: {TotalPrice} THB",
                auditRecord.Id,
                message.FileId,
                result.TotalPrice);

            // 6. Publish PriceCalculatedEvent for QuotationService to consume
            await _publishEndpoint.Publish(new PriceCalculatedEvent
            {
                PricingAuditId = auditRecord.Id,
                FileId = message.FileId,
                CustomerId = message.CustomerId,
                MaterialId = material.Id,
                ProcessId = process.Id,
                Quantity = message.Quantity ?? 1,
                Strategy = result.Strategy.ToString(),
                Breakdown = new PriceBreakdownContract
                {
                    MaterialCost = result.MaterialCost,
                    SupportCost = result.SupportMaterialCost,
                    MachineTimeCost = result.MachineTimeCost,
                    SetupCost = result.SetupCost,
                    ComplexitySurcharge = result.ComplexitySurcharge,
                    MarginAmount = result.MarginAmount,
                    TotalPrice = result.TotalPrice
                },
                Currency = "THB",
                ConfidenceLevel = result.ConfidenceLevel,
                ValidUntil = auditRecord.ValidUntil,
                CalculatedAt = auditRecord.CalculatedAt
            }, cancellationToken);

            _logger.LogInformation(
                "Published PriceCalculatedEvent for file {FileId}",
                message.FileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing FileAnalyzedEvent for file {FileId}",
                message.FileId);
            throw; // Rethrow to trigger retry
        }
    }
}
```

### Task 5.3: Create OrderCompletedEventConsumer

**File**: `Maliev.PricingService.Api/Consumers/OrderCompletedEventConsumer.cs`

```csharp
using Maliev.MessagingContracts.Contracts;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Consumers;

/// <summary>
/// Consumes OrderCompletedEvent to update training data with actual job outcomes.
/// This enables ML model improvement based on real production data.
/// </summary>
public class OrderCompletedEventConsumer : IConsumer<OrderCompletedEvent>
{
    private readonly PricingDbContext _dbContext;
    private readonly ILogger<OrderCompletedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderCompletedEventConsumer"/> class.
    /// </summary>
    public OrderCompletedEventConsumer(
        PricingDbContext dbContext,
        ILogger<OrderCompletedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Received OrderCompletedEvent for order {OrderId}, quotation {QuotationId}",
            message.OrderId,
            message.QuotationId);

        try
        {
            // Find the pricing audit record linked to this quotation
            var auditRecord = await _dbContext.PricingAuditRecords
                .Include(a => a.TrainingData)
                .FirstOrDefaultAsync(a => a.QuotationId == message.QuotationId, cancellationToken);

            if (auditRecord == null)
            {
                _logger.LogWarning(
                    "No pricing audit record found for quotation {QuotationId}",
                    message.QuotationId);
                return;
            }

            // Create or update training data
            var trainingData = auditRecord.TrainingData ?? new PricingTrainingData
            {
                Id = Guid.NewGuid(),
                PricingAuditRecordId = auditRecord.Id
            };

            trainingData.OrderId = message.OrderId;
            trainingData.CustomerAccepted = true;
            trainingData.AcceptedAt = message.OrderCreatedAt;
            trainingData.JobCompleted = true;
            trainingData.CompletedAt = message.CompletedAt;
            trainingData.JobSucceeded = message.JobSucceeded;
            trainingData.ActualMaterialUsedCm3 = message.ActualMaterialUsedCm3;
            trainingData.ActualPrintTimeHours = message.ActualPrintTimeHours;
            trainingData.ActualLaborHours = message.ActualLaborHours;
            trainingData.ActualTotalCost = message.ActualTotalCost;

            // Calculate actual profit margin
            if (message.ActualTotalCost.HasValue && auditRecord.TotalPrice > 0)
            {
                trainingData.ActualProfitMargin =
                    (auditRecord.TotalPrice - message.ActualTotalCost.Value) / auditRecord.TotalPrice;
            }

            if (auditRecord.TrainingData == null)
            {
                _dbContext.PricingTrainingData.Add(trainingData);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated training data for audit record {AuditRecordId}, actual margin: {Margin:P2}",
                auditRecord.Id,
                trainingData.ActualProfitMargin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing OrderCompletedEvent for order {OrderId}",
                message.OrderId);
            throw;
        }
    }
}
```

**Checklist**:
- [ ] FileAnalyzedEventConsumer fully implemented
- [ ] OrderCompletedEventConsumer implemented
- [ ] Service client dependencies used
- [ ] Audit record creation complete
- [ ] PriceCalculatedEvent publishing implemented
- [ ] Training data collection implemented
- [ ] Error handling with retry support
- [ ] XML documentation on all members

---

## Phase 6: Update MessagingContracts

### Task 6.1: Add Pricing Event Schemas

**File**: `Maliev.MessagingContracts/contracts/schemas/pricing/pricing-events.yaml`

```yaml
asyncapi: 3.0.0
info:
  title: Maliev Pricing Events
  version: 1.0.0
  description: |
    Events published by the PricingService for instant quotation workflow.

    Event flow:
    1. GeometryService publishes FileAnalyzedEvent
    2. PricingService consumes and calculates price
    3. PricingService publishes PriceCalculatedEvent
    4. QuotationService consumes and creates draft quotation

channels:
  pricing-price-calculated:
    address: pricing-price-calculated
    messages:
      PriceCalculatedEvent:
        $ref: '#/components/messages/PriceCalculatedEvent'

  pricing-configuration-changed:
    address: pricing-configuration-changed
    messages:
      PricingConfigurationChangedEvent:
        $ref: '#/components/messages/PricingConfigurationChangedEvent'

components:
  schemas:
    BaseMessage:
      type: object
      required:
        - messageId
        - timestamp
        - source
      properties:
        messageId:
          type: string
          format: uuid
        timestamp:
          type: string
          format: date-time
        source:
          type: string
        correlationId:
          type: string
          format: uuid

    PriceBreakdown:
      type: object
      required:
        - materialCost
        - supportCost
        - machineTimeCost
        - setupCost
        - complexitySurcharge
        - marginAmount
        - totalPrice
      properties:
        materialCost:
          type: number
          format: decimal
        supportCost:
          type: number
          format: decimal
        machineTimeCost:
          type: number
          format: decimal
        setupCost:
          type: number
          format: decimal
        complexitySurcharge:
          type: number
          format: decimal
        marginAmount:
          type: number
          format: decimal
        totalPrice:
          type: number
          format: decimal

    PriceCalculatedEvent:
      allOf:
        - $ref: '#/components/schemas/BaseMessage'
        - type: object
          required:
            - pricingAuditId
            - fileId
            - customerId
            - materialId
            - processId
            - quantity
            - strategy
            - breakdown
            - currency
            - confidenceLevel
            - validUntil
            - calculatedAt
          properties:
            pricingAuditId:
              type: string
              format: uuid
            fileId:
              type: string
              format: uuid
            customerId:
              type: string
              format: uuid
            materialId:
              type: string
              format: uuid
            processId:
              type: string
              format: uuid
            quantity:
              type: integer
              minimum: 1
            strategy:
              type: string
              enum: [RuleBased, MLEnhanced, Manual, Hybrid]
            breakdown:
              $ref: '#/components/schemas/PriceBreakdown'
            currency:
              type: string
              maxLength: 3
            confidenceLevel:
              type: number
              format: decimal
              minimum: 0
              maximum: 1
            validUntil:
              type: string
              format: date-time
            calculatedAt:
              type: string
              format: date-time

    PricingConfigurationChangedEvent:
      allOf:
        - $ref: '#/components/schemas/BaseMessage'
        - type: object
          required:
            - configurationId
            - materialId
            - processId
            - changeType
            - changedBy
            - changedAt
          properties:
            configurationId:
              type: string
              format: uuid
            materialId:
              type: string
              format: uuid
            processId:
              type: string
              format: uuid
            changeType:
              type: string
              enum: [Created, Updated, Deactivated]
            changedBy:
              type: string
            changedAt:
              type: string
              format: date-time

  messages:
    PriceCalculatedEvent:
      name: PriceCalculatedEvent
      title: Price Calculated Event
      summary: Published when an instant price has been calculated for a 3D file
      contentType: application/json
      payload:
        $ref: '#/components/schemas/PriceCalculatedEvent'

    PricingConfigurationChangedEvent:
      name: PricingConfigurationChangedEvent
      title: Pricing Configuration Changed Event
      summary: Published when a pricing configuration is created, updated, or deactivated
      contentType: application/json
      payload:
        $ref: '#/components/schemas/PricingConfigurationChangedEvent'
```

### Task 6.2: Add FileAnalyzedEvent Extension

**File**: `Maliev.MessagingContracts/Contracts/Geometry/FileAnalyzedEvent.cs`

```csharp
namespace Maliev.MessagingContracts.Contracts.Geometry;

/// <summary>
/// Event published when a 3D file has been analyzed by GeometryService.
/// Extended to support pricing integration.
/// </summary>
public record FileAnalyzedEvent
{
    /// <summary>Unique identifier for this message.</summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>When the event was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Source service name.</summary>
    public string Source { get; init; } = "GeometryService";

    /// <summary>Correlation ID for distributed tracing.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>File ID that was analyzed.</summary>
    public required Guid FileId { get; init; }

    /// <summary>Customer ID who uploaded the file.</summary>
    public required Guid CustomerId { get; init; }

    /// <summary>Optional material ID selected by customer.</summary>
    public Guid? MaterialId { get; init; }

    /// <summary>Optional manufacturing process ID selected by customer.</summary>
    public Guid? ManufacturingProcessId { get; init; }

    /// <summary>Optional quantity requested.</summary>
    public int? Quantity { get; init; }

    /// <summary>Volume in cubic centimeters.</summary>
    public required decimal VolumeCm3 { get; init; }

    /// <summary>Estimated support volume in cubic centimeters.</summary>
    public decimal? SupportVolumeCm3 { get; init; }

    /// <summary>Surface area in square centimeters.</summary>
    public required decimal SurfaceAreaCm2 { get; init; }

    /// <summary>Bounding box X dimension in millimeters.</summary>
    public required decimal BoundingBoxX { get; init; }

    /// <summary>Bounding box Y dimension in millimeters.</summary>
    public required decimal BoundingBoxY { get; init; }

    /// <summary>Bounding box Z dimension in millimeters.</summary>
    public required decimal BoundingBoxZ { get; init; }

    /// <summary>Whether the mesh is watertight/manifold.</summary>
    public required bool IsManifold { get; init; }

    /// <summary>Number of triangles in the mesh.</summary>
    public required int TriangleCount { get; init; }

    /// <summary>Euler number for topology validation.</summary>
    public int? EulerNumber { get; init; }
}
```

### Task 6.3: Add OrderCompletedEvent Extension

**File**: `Maliev.MessagingContracts/Contracts/Orders/OrderCompletedEvent.cs`

```csharp
namespace Maliev.MessagingContracts.Contracts.Orders;

/// <summary>
/// Event published when an order has been completed.
/// Extended to support pricing training data collection.
/// </summary>
public record OrderCompletedEvent
{
    /// <summary>Unique identifier for this message.</summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>When the event was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Source service name.</summary>
    public string Source { get; init; } = "OrderService";

    /// <summary>Order ID that was completed.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Quotation ID the order was created from.</summary>
    public required Guid QuotationId { get; init; }

    /// <summary>When the order was created.</summary>
    public required DateTime OrderCreatedAt { get; init; }

    /// <summary>When the order was completed.</summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>Whether the manufacturing job succeeded.</summary>
    public required bool JobSucceeded { get; init; }

    /// <summary>Actual material used in cubic centimeters.</summary>
    public decimal? ActualMaterialUsedCm3 { get; init; }

    /// <summary>Actual print time in hours.</summary>
    public decimal? ActualPrintTimeHours { get; init; }

    /// <summary>Actual labor hours spent.</summary>
    public decimal? ActualLaborHours { get; init; }

    /// <summary>Actual total cost of the job.</summary>
    public decimal? ActualTotalCost { get; init; }
}
```

### Task 6.4: Add C# PriceCalculatedEvent Class

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

```csharp
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.QuotationService.Data;
using Maliev.QuotationService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.QuotationService.Api.Consumers;

/// <summary>
/// Consumes PriceCalculatedEvent from PricingService and creates draft quotations.
/// This enables automatic quotation creation from instant pricing.
/// </summary>
public class PriceCalculatedEventConsumer : IConsumer<PriceCalculatedEvent>
{
    private readonly QuotationDbContext _dbContext;
    private readonly ILogger<PriceCalculatedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceCalculatedEventConsumer"/> class.
    /// </summary>
    public PriceCalculatedEventConsumer(
        QuotationDbContext dbContext,
        ILogger<PriceCalculatedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<PriceCalculatedEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Received PriceCalculatedEvent for file {FileId}, customer {CustomerId}",
            message.FileId,
            message.CustomerId);

        try
        {
            // Check if a draft quotation already exists for this file
            var existingQuotation = await _dbContext.Quotations
                .FirstOrDefaultAsync(q =>
                    q.CustomerId == message.CustomerId &&
                    q.Status == QuotationStatus.Draft &&
                    q.LineItems.Any(li => li.FileId == message.FileId),
                    cancellationToken);

            if (existingQuotation != null)
            {
                // Update existing line item with new price
                var lineItem = existingQuotation.LineItems
                    .FirstOrDefault(li => li.FileId == message.FileId);

                if (lineItem != null)
                {
                    lineItem.UnitPrice = message.Breakdown.TotalPrice / message.Quantity;
                    lineItem.Quantity = message.Quantity;
                    lineItem.LineTotal = message.Breakdown.TotalPrice;
                    lineItem.PricingAuditId = message.PricingAuditId;
                    lineItem.UpdatedAt = DateTime.UtcNow;
                }

                existingQuotation.UpdatedAt = DateTime.UtcNow;
                existingQuotation.ValidUntil = message.ValidUntil;
            }
            else
            {
                // Create new draft quotation
                var quotation = new Quotation
                {
                    Id = Guid.NewGuid(),
                    CustomerId = message.CustomerId,
                    Status = QuotationStatus.Draft,
                    Currency = message.Currency,
                    ValidUntil = message.ValidUntil,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "PricingService",
                    LineItems =
                    [
                        new QuotationLineItem
                        {
                            Id = Guid.NewGuid(),
                            FileId = message.FileId,
                            MaterialId = message.MaterialId,
                            ProcessId = message.ProcessId,
                            Quantity = message.Quantity,
                            UnitPrice = message.Breakdown.TotalPrice / message.Quantity,
                            LineTotal = message.Breakdown.TotalPrice,
                            PricingAuditId = message.PricingAuditId,
                            CreatedAt = DateTime.UtcNow
                        }
                    ]
                };

                _dbContext.Quotations.Add(quotation);

                _logger.LogInformation(
                    "Created draft quotation {QuotationId} for file {FileId}",
                    quotation.Id,
                    message.FileId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing PriceCalculatedEvent for file {FileId}",
                message.FileId);
            throw;
        }
    }
}
```

### Task 7.2: Update QuotationService Program.cs

Add the consumer registration to QuotationService's `Program.cs`:

```csharp
// In QuotationService Program.cs, update MassTransit configuration:
builder.AddMassTransitWithRabbitMq(cfg =>
{
    // Existing consumers...
    cfg.AddConsumer<PriceCalculatedEventConsumer>();
});
```

**Checklist**:
- [ ] Consumer created in QuotationService
- [ ] Program.cs updated to register consumer
- [ ] Draft quotation creation logic implemented
- [ ] Existing quotation update logic implemented
- [ ] PricingAuditId linked to line items

---

## Phase 8: Historical Data Migration

### Task 8.1: Create Migration Script

**File**: `Maliev.PricingService.Data/Migrations/Scripts/migrate_historical_data.sql`

```sql
-- ============================================================================
-- Historical Data Migration Script for PricingService
-- Migrates data from legacy PostgreSQL database to pricing_app_db
-- ============================================================================

-- Run this script in the pricing_app_db database AFTER the EF migrations

-- Step 1: Create temporary staging table for legacy data
CREATE TEMP TABLE legacy_quotes_staging (
    legacy_id UUID,
    file_id UUID NOT NULL,
    customer_id UUID NOT NULL,
    volume_cm3 DECIMAL(18, 6),
    support_volume_cm3 DECIMAL(18, 6),
    surface_area_cm2 DECIMAL(18, 6),
    bounding_box_x DECIMAL(10, 2),
    bounding_box_y DECIMAL(10, 2),
    bounding_box_z DECIMAL(10, 2),
    is_manifold BOOLEAN DEFAULT TRUE,
    triangle_count INTEGER DEFAULT 0,
    material_id UUID,
    material_code VARCHAR(50),
    process_id UUID,
    process_name VARCHAR(100),
    quantity INTEGER DEFAULT 1,
    quoted_price DECIMAL(18, 2),
    actual_cost DECIMAL(18, 2),
    customer_accepted BOOLEAN DEFAULT FALSE,
    job_completed BOOLEAN DEFAULT FALSE,
    job_succeeded BOOLEAN DEFAULT FALSE,
    actual_material_cm3 DECIMAL(18, 6),
    actual_print_hours DECIMAL(10, 2),
    quoted_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
);

-- Step 2: Import data from legacy database
-- This uses postgres_fdw to connect to the legacy database
-- Adjust connection details as needed

-- First, create the foreign data wrapper (run as superuser)
-- CREATE EXTENSION IF NOT EXISTS postgres_fdw;
--
-- CREATE SERVER legacy_server
--     FOREIGN DATA WRAPPER postgres_fdw
--     OPTIONS (host 'legacy-db-host', dbname 'legacy_db', port '5432');
--
-- CREATE USER MAPPING FOR CURRENT_USER
--     SERVER legacy_server
--     OPTIONS (user 'readonly_user', password 'password');

-- Import data from legacy database
-- INSERT INTO legacy_quotes_staging (
--     legacy_id, file_id, customer_id, volume_cm3, support_volume_cm3,
--     surface_area_cm2, bounding_box_x, bounding_box_y, bounding_box_z,
--     is_manifold, triangle_count, material_id, material_code, process_id,
--     process_name, quantity, quoted_price, actual_cost, customer_accepted,
--     job_completed, job_succeeded, actual_material_cm3, actual_print_hours,
--     quoted_at, completed_at
-- )
-- SELECT
--     q.id,
--     q.file_id,
--     q.customer_id,
--     COALESCE(f.volume_cm3, 0),
--     COALESCE(f.support_volume_cm3, 0),
--     COALESCE(f.surface_area_cm2, 0),
--     COALESCE(f.bounding_x_mm, 0) / 10,
--     COALESCE(f.bounding_y_mm, 0) / 10,
--     COALESCE(f.bounding_z_mm, 0) / 10,
--     COALESCE(f.is_manifold, TRUE),
--     COALESCE(f.triangle_count, 0),
--     q.material_id,
--     m.code,
--     q.process_id,
--     p.name,
--     q.quantity,
--     q.total_price,
--     o.actual_cost,
--     o.id IS NOT NULL,
--     o.status = 'Completed',
--     o.status = 'Completed' AND NOT o.has_defects,
--     o.actual_material_cm3,
--     o.actual_print_hours,
--     q.created_at,
--     o.completed_at
-- FROM legacy_quotes q
-- LEFT JOIN legacy_files f ON q.file_id = f.id
-- LEFT JOIN legacy_materials m ON q.material_id = m.id
-- LEFT JOIN legacy_processes p ON q.process_id = p.id
-- LEFT JOIN legacy_orders o ON q.id = o.quotation_id
-- WHERE q.created_at >= NOW() - INTERVAL '7 years';

-- Step 3: Create a default pricing configuration for historical data
INSERT INTO pricing_configurations (
    id,
    material_id,
    manufacturing_process_id,
    material_price_per_cm3,
    support_material_price_per_cm3,
    density_gram_per_cm3,
    machine_hourly_rate,
    print_speed_cm3_per_hour,
    setup_cost_flat,
    minimum_order_price,
    margin_multiplier,
    complexity_threshold,
    complexity_surcharge_percent,
    effective_from,
    is_active,
    created_at,
    created_by
)
VALUES (
    gen_random_uuid(),
    '00000000-0000-0000-0000-000000000001', -- Default FDM material placeholder
    '00000000-0000-0000-0000-000000000001', -- Default FDM process placeholder
    0.05,  -- 0.05 THB per cm3 (default PLA)
    0.03,  -- 0.03 THB per cm3 for support
    1.24,  -- PLA density
    500.0, -- 500 THB per hour
    15.0,  -- 15 cm3/hour print speed
    50.0,  -- 50 THB setup cost
    100.0, -- 100 THB minimum order
    1.5,   -- 50% margin
    2.0,   -- Complexity threshold
    15.0,  -- 15% complexity surcharge
    '2020-01-01',
    TRUE,
    NOW(),
    'migration_script'
)
ON CONFLICT DO NOTHING;

-- Step 4: Insert audit records from staging data
INSERT INTO pricing_audit_records (
    id,
    file_id,
    customer_id,
    input_volume_cm3,
    input_support_volume_cm3,
    input_surface_area_cm2,
    input_bounding_box_x,
    input_bounding_box_y,
    input_bounding_box_z,
    input_is_manifold,
    input_triangle_count,
    material_id,
    material_code,
    manufacturing_process_id,
    manufacturing_process_name,
    quantity,
    pricing_configuration_id,
    config_material_price_per_cm3,
    config_support_price_per_cm3,
    config_machine_hourly_rate,
    config_margin_multiplier,
    strategy,
    material_cost,
    support_material_cost,
    machine_time_cost,
    setup_cost,
    complexity_surcharge,
    subtotal_before_margin,
    margin_amount,
    total_unit_price,
    total_price,
    confidence_level,
    currency_code,
    valid_from,
    valid_until,
    calculated_at,
    calculated_by_system,
    calculation_duration
)
SELECT
    gen_random_uuid(),
    s.file_id,
    s.customer_id,
    COALESCE(s.volume_cm3, 0),
    COALESCE(s.support_volume_cm3, 0),
    COALESCE(s.surface_area_cm2, 0),
    COALESCE(s.bounding_box_x, 0),
    COALESCE(s.bounding_box_y, 0),
    COALESCE(s.bounding_box_z, 0),
    COALESCE(s.is_manifold, TRUE),
    COALESCE(s.triangle_count, 0),
    COALESCE(s.material_id, '00000000-0000-0000-0000-000000000001'),
    COALESCE(s.material_code, 'UNKNOWN'),
    COALESCE(s.process_id, '00000000-0000-0000-0000-000000000001'),
    COALESCE(s.process_name, 'Unknown Process'),
    COALESCE(s.quantity, 1),
    (SELECT id FROM pricing_configurations LIMIT 1),
    0.05,
    0.03,
    500.0,
    1.5,
    3, -- Strategy = Manual (historical data)
    s.quoted_price * 0.3, -- Estimate: 30% material
    s.quoted_price * 0.05, -- Estimate: 5% support
    s.quoted_price * 0.25, -- Estimate: 25% machine time
    50.0,
    0, -- No complexity surcharge recorded
    s.quoted_price / 1.5, -- Reverse-calculate subtotal
    s.quoted_price - (s.quoted_price / 1.5), -- Margin
    s.quoted_price / GREATEST(s.quantity, 1),
    s.quoted_price,
    0.5, -- Low confidence for historical data
    'THB',
    COALESCE(s.quoted_at, NOW()),
    COALESCE(s.quoted_at, NOW()) + INTERVAL '7 days',
    COALESCE(s.quoted_at, NOW()),
    'migration_script',
    INTERVAL '0 seconds'
FROM legacy_quotes_staging s
WHERE s.quoted_price IS NOT NULL;

-- Step 5: Insert training data for completed jobs
INSERT INTO pricing_training_data (
    id,
    pricing_audit_record_id,
    order_id,
    customer_accepted,
    accepted_at,
    job_completed,
    completed_at,
    job_succeeded,
    actual_material_used_cm3,
    actual_print_time_hours,
    actual_total_cost,
    actual_profit_margin,
    used_for_training
)
SELECT
    gen_random_uuid(),
    a.id,
    NULL, -- Order ID not preserved in migration
    s.customer_accepted,
    s.quoted_at,
    s.job_completed,
    s.completed_at,
    s.job_succeeded,
    s.actual_material_cm3,
    s.actual_print_hours,
    s.actual_cost,
    CASE
        WHEN s.actual_cost IS NOT NULL AND s.quoted_price > 0
        THEN (s.quoted_price - s.actual_cost) / s.quoted_price
        ELSE NULL
    END,
    FALSE -- Not yet used for training
FROM legacy_quotes_staging s
JOIN pricing_audit_records a ON a.file_id = s.file_id AND a.customer_id = s.customer_id
WHERE s.job_completed = TRUE;

-- Step 6: Cleanup
DROP TABLE IF EXISTS legacy_quotes_staging;

-- Step 7: Verify migration
SELECT 'Audit Records Migrated' AS metric, COUNT(*) AS count FROM pricing_audit_records WHERE calculated_by_system = 'migration_script'
UNION ALL
SELECT 'Training Data Records' AS metric, COUNT(*) AS count FROM pricing_training_data WHERE used_for_training = FALSE
UNION ALL
SELECT 'Accepted Jobs' AS metric, COUNT(*) AS count FROM pricing_training_data WHERE customer_accepted = TRUE
UNION ALL
SELECT 'Completed Jobs' AS metric, COUNT(*) AS count FROM pricing_training_data WHERE job_completed = TRUE
UNION ALL
SELECT 'Successful Jobs' AS metric, COUNT(*) AS count FROM pricing_training_data WHERE job_succeeded = TRUE;
```

### Task 8.2: Create Data Validation Queries

**File**: `Maliev.PricingService.Data/Migrations/Scripts/validate_migration.sql`

```sql
-- ============================================================================
-- Data Validation Queries for Historical Migration
-- Run these queries to verify migration integrity
-- ============================================================================

-- 1. Check for missing required fields
SELECT
    'Missing FileId' AS issue,
    COUNT(*) AS count
FROM pricing_audit_records
WHERE file_id IS NULL
UNION ALL
SELECT
    'Missing CustomerId' AS issue,
    COUNT(*) AS count
FROM pricing_audit_records
WHERE customer_id IS NULL
UNION ALL
SELECT
    'Zero or Negative Price' AS issue,
    COUNT(*) AS count
FROM pricing_audit_records
WHERE total_price <= 0;

-- 2. Identify outliers (prices more than 3 standard deviations from mean)
WITH stats AS (
    SELECT
        AVG(total_price) AS avg_price,
        STDDEV(total_price) AS stddev_price
    FROM pricing_audit_records
    WHERE calculated_by_system = 'migration_script'
)
SELECT
    a.id,
    a.file_id,
    a.total_price,
    s.avg_price,
    s.stddev_price,
    (a.total_price - s.avg_price) / NULLIF(s.stddev_price, 0) AS z_score
FROM pricing_audit_records a
CROSS JOIN stats s
WHERE calculated_by_system = 'migration_script'
  AND ABS(a.total_price - s.avg_price) > 3 * s.stddev_price
ORDER BY z_score DESC
LIMIT 100;

-- 3. Check profit margin distribution
SELECT
    CASE
        WHEN actual_profit_margin IS NULL THEN 'Unknown'
        WHEN actual_profit_margin < 0 THEN 'Negative (Loss)'
        WHEN actual_profit_margin < 0.2 THEN 'Low (<20%)'
        WHEN actual_profit_margin < 0.4 THEN 'Normal (20-40%)'
        WHEN actual_profit_margin < 0.6 THEN 'High (40-60%)'
        ELSE 'Very High (>60%)'
    END AS margin_bucket,
    COUNT(*) AS count,
    ROUND(AVG(actual_profit_margin) * 100, 2) AS avg_margin_percent
FROM pricing_training_data
GROUP BY 1
ORDER BY 2 DESC;

-- 4. Check data completeness for ML training
SELECT
    'Total Records' AS metric,
    COUNT(*) AS count
FROM pricing_training_data
UNION ALL
SELECT
    'With Actual Cost' AS metric,
    COUNT(*) AS count
FROM pricing_training_data
WHERE actual_total_cost IS NOT NULL
UNION ALL
SELECT
    'With Material Usage' AS metric,
    COUNT(*) AS count
FROM pricing_training_data
WHERE actual_material_used_cm3 IS NOT NULL
UNION ALL
SELECT
    'With Print Time' AS metric,
    COUNT(*) AS count
FROM pricing_training_data
WHERE actual_print_time_hours IS NOT NULL;

-- 5. Verify referential integrity
SELECT
    'Orphan Training Data' AS issue,
    COUNT(*) AS count
FROM pricing_training_data t
LEFT JOIN pricing_audit_records a ON t.pricing_audit_record_id = a.id
WHERE a.id IS NULL;
```

**Checklist**:
- [ ] Migration script created
- [ ] postgres_fdw extension configured (if needed)
- [ ] Test run on development database
- [ ] Validation queries executed
- [ ] Outliers reviewed and flagged
- [ ] Referential integrity verified

---

## Phase 9: Testing

### Task 9.1: Create Test Factory

**File**: `Maliev.PricingService.Tests/TestFixtures/PricingServiceTestFactory.cs`

```csharp
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Maliev.PricingService.Data;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Maliev.PricingService.Tests.TestFixtures;

/// <summary>
/// Test factory for PricingService integration tests using Testcontainers.
/// </summary>
public class PricingServiceTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_test_db")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:8.4-alpine")
        .Build();

    /// <summary>
    /// Initializes the test containers.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    /// <summary>
    /// Disposes the test containers.
    /// </summary>
    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registration
            services.RemoveAll<DbContextOptions<PricingDbContext>>();
            services.RemoveAll<PricingDbContext>();

            // Add test DbContext with Testcontainers connection
            services.AddDbContext<PricingDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            // Configure Redis
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = _redisContainer.GetConnectionString();
            });

            // Add MassTransit Test Harness
            services.AddMassTransitTestHarness();

            // Ensure database is created and migrated
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
            db.Database.EnsureCreated();
        });
    }

    /// <summary>
    /// Creates an HTTP client with test authentication.
    /// </summary>
    /// <param name="claims">Optional claims to include in the test JWT.</param>
    /// <returns>Configured HTTP client.</returns>
    public HttpClient CreateAuthenticatedClient(Dictionary<string, string>? claims = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateTestJwt(claims));
        return client;
    }

    /// <summary>
    /// Seeds test data into the database.
    /// </summary>
    public async Task SeedTestDataAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();

        // Add test pricing configuration
        var config = new Data.Entities.PricingConfiguration
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            MaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ManufacturingProcessId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MaterialPricePerCm3 = 0.05m,
            SupportMaterialPricePerCm3 = 0.03m,
            DensityGramPerCm3 = 1.24m,
            MachineHourlyRate = 500m,
            PrintSpeedCm3PerHour = 15m,
            SetupCostFlat = 50m,
            MinimumOrderPrice = 100m,
            MarginMultiplier = 1.5m,
            ComplexityThreshold = 2.0m,
            ComplexitySurchargePercent = 15m,
            EffectiveFrom = DateTime.UtcNow.AddYears(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };

        db.PricingConfigurations.Add(config);
        await db.SaveChangesAsync();
    }

    private static string CreateTestJwt(Dictionary<string, string>? claims = null)
    {
        // In a real implementation, this would generate a valid test JWT
        // For testing purposes, the TestAuthHandler bypasses JWT validation
        return "test-jwt-token";
    }
}
```

### Task 9.2: Create Integration Tests

**File**: `Maliev.PricingService.Tests/Integration/PricingControllerTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using Maliev.PricingService.Api.DTOs;
using Maliev.PricingService.Tests.TestFixtures;
using Xunit;

namespace Maliev.PricingService.Tests.Integration;

/// <summary>
/// Integration tests for PricingController.
/// </summary>
public class PricingControllerTests : IClassFixture<PricingServiceTestFactory>
{
    private readonly PricingServiceTestFactory _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingControllerTests"/> class.
    /// </summary>
    public PricingControllerTests(PricingServiceTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    /// <summary>
    /// Verifies that the calculate endpoint returns a valid price breakdown.
    /// </summary>
    [Fact]
    public async Task CalculatePrice_WithValidInput_ReturnsBreakdown()
    {
        // Arrange
        await _factory.SeedTestDataAsync();

        var request = new CalculatePriceRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MaterialCode = "PLA-WHITE",
            ManufacturingProcessId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ManufacturingProcessName = "FDM",
            Quantity = 1,
            VolumeCm3 = 100m,
            SupportVolumeCm3 = 10m,
            SurfaceAreaCm2 = 500m,
            BoundingBoxX = 10m,
            BoundingBoxY = 10m,
            BoundingBoxZ = 10m,
            IsManifold = true,
            TriangleCount = 5000
        };

        // Act
        var response = await _client.PostAsJsonAsync("/pricing/v1/calculate", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CalculatePriceResponse>();
        Assert.NotNull(result);
        Assert.True(result.TotalPrice > 0);
        Assert.True(result.MaterialCost > 0);
        Assert.NotEqual(Guid.Empty, result.AuditId);
    }

    /// <summary>
    /// Verifies that the calculate endpoint returns 404 when no pricing config exists.
    /// </summary>
    [Fact]
    public async Task CalculatePrice_WithUnknownMaterial_ReturnsNotFound()
    {
        // Arrange
        var request = new CalculatePriceRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(), // Unknown material
            MaterialCode = "UNKNOWN",
            ManufacturingProcessId = Guid.NewGuid(), // Unknown process
            ManufacturingProcessName = "UNKNOWN",
            Quantity = 1,
            VolumeCm3 = 100m,
            SupportVolumeCm3 = 10m,
            SurfaceAreaCm2 = 500m,
            BoundingBoxX = 10m,
            BoundingBoxY = 10m,
            BoundingBoxZ = 10m,
            IsManifold = true,
            TriangleCount = 5000
        };

        // Act
        var response = await _client.PostAsJsonAsync("/pricing/v1/calculate", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that the audit endpoint returns the audit record.
    /// </summary>
    [Fact]
    public async Task GetAuditRecord_WithValidId_ReturnsRecord()
    {
        // Arrange
        await _factory.SeedTestDataAsync();

        // First, create an audit record via calculate
        var calculateRequest = new CalculatePriceRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MaterialCode = "PLA-WHITE",
            ManufacturingProcessId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ManufacturingProcessName = "FDM",
            Quantity = 1,
            VolumeCm3 = 50m,
            SupportVolumeCm3 = 5m,
            SurfaceAreaCm2 = 200m,
            BoundingBoxX = 5m,
            BoundingBoxY = 5m,
            BoundingBoxZ = 5m,
            IsManifold = true,
            TriangleCount = 2000
        };

        var calculateResponse = await _client.PostAsJsonAsync("/pricing/v1/calculate", calculateRequest);
        var calculateResult = await calculateResponse.Content.ReadFromJsonAsync<CalculatePriceResponse>();

        // Act
        var response = await _client.GetAsync($"/pricing/v1/audit/{calculateResult!.AuditId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = await response.Content.ReadFromJsonAsync<PricingAuditResponse>();
        Assert.NotNull(audit);
        Assert.Equal(calculateResult.AuditId, audit.Id);
        Assert.Equal(calculateRequest.VolumeCm3, audit.InputVolumeCm3);
    }

    /// <summary>
    /// Verifies that complexity surcharge is applied for complex parts.
    /// </summary>
    [Fact]
    public async Task CalculatePrice_WithComplexPart_AppliesComplexitySurcharge()
    {
        // Arrange
        await _factory.SeedTestDataAsync();

        // High surface area to volume ratio = complex part
        var request = new CalculatePriceRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MaterialCode = "PLA-WHITE",
            ManufacturingProcessId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ManufacturingProcessName = "FDM",
            Quantity = 1,
            VolumeCm3 = 10m, // Small volume
            SupportVolumeCm3 = 2m,
            SurfaceAreaCm2 = 500m, // Large surface area = high complexity ratio
            BoundingBoxX = 5m,
            BoundingBoxY = 5m,
            BoundingBoxZ = 5m,
            IsManifold = true,
            TriangleCount = 10000
        };

        // Act
        var response = await _client.PostAsJsonAsync("/pricing/v1/calculate", request);
        var result = await response.Content.ReadFromJsonAsync<CalculatePriceResponse>();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ComplexitySurcharge > 0, "Complexity surcharge should be applied for complex parts");
    }

    /// <summary>
    /// Verifies that quantity affects total price correctly.
    /// </summary>
    [Fact]
    public async Task CalculatePrice_WithQuantity_CalculatesCorrectTotal()
    {
        // Arrange
        await _factory.SeedTestDataAsync();

        var singleRequest = new CalculatePriceRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MaterialCode = "PLA-WHITE",
            ManufacturingProcessId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ManufacturingProcessName = "FDM",
            Quantity = 1,
            VolumeCm3 = 50m,
            SupportVolumeCm3 = 5m,
            SurfaceAreaCm2 = 100m,
            BoundingBoxX = 5m,
            BoundingBoxY = 5m,
            BoundingBoxZ = 5m,
            IsManifold = true,
            TriangleCount = 2000
        };

        var multiRequest = new CalculatePriceRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            MaterialCode = "PLA-WHITE",
            ManufacturingProcessId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ManufacturingProcessName = "FDM",
            Quantity = 5, // 5 units
            VolumeCm3 = 50m,
            SupportVolumeCm3 = 5m,
            SurfaceAreaCm2 = 100m,
            BoundingBoxX = 5m,
            BoundingBoxY = 5m,
            BoundingBoxZ = 5m,
            IsManifold = true,
            TriangleCount = 2000
        };

        // Act
        var singleResponse = await _client.PostAsJsonAsync("/pricing/v1/calculate", singleRequest);
        var singleResult = await singleResponse.Content.ReadFromJsonAsync<CalculatePriceResponse>();

        var multiResponse = await _client.PostAsJsonAsync("/pricing/v1/calculate", multiRequest);
        var multiResult = await multiResponse.Content.ReadFromJsonAsync<CalculatePriceResponse>();

        // Assert
        Assert.NotNull(singleResult);
        Assert.NotNull(multiResult);

        // Total price for 5 units should be approximately 5x single unit
        // (may have slight differences due to fixed setup costs)
        var expectedMinimum = singleResult.TotalUnitPrice * 5;
        Assert.True(multiResult.TotalPrice >= expectedMinimum * 0.9m,
            $"Multi-unit price ({multiResult.TotalPrice}) should be close to 5x unit price ({expectedMinimum})");
    }
}
```

### Task 9.3: Create Pricing Engine Unit Tests

**File**: `Maliev.PricingService.Tests/Unit/RuleBasedPricingEngineTests.cs`

```csharp
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data.Entities;
using Xunit;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Unit tests for RuleBasedPricingEngine.
/// </summary>
public class RuleBasedPricingEngineTests
{
    private readonly RuleBasedPricingEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleBasedPricingEngineTests"/> class.
    /// </summary>
    public RuleBasedPricingEngineTests()
    {
        _engine = new RuleBasedPricingEngine();
    }

    /// <summary>
    /// Verifies that material cost is calculated correctly.
    /// </summary>
    [Fact]
    public async Task Calculate_MaterialCost_IsCorrect()
    {
        // Arrange
        var request = CreateTestRequest(volumeCm3: 100m);
        var config = CreateTestConfig(materialPricePerCm3: 0.10m);

        // Act
        var result = await _engine.CalculateAsync(request, config, CancellationToken.None);

        // Assert
        // Material cost = volume * density * price/cm3
        var expectedMaterialCost = 100m * config.DensityGramPerCm3 * 0.10m;
        Assert.Equal(expectedMaterialCost, result.MaterialCost, 2);
    }

    /// <summary>
    /// Verifies that machine time cost is calculated correctly.
    /// </summary>
    [Fact]
    public async Task Calculate_MachineTimeCost_IsCorrect()
    {
        // Arrange
        var request = CreateTestRequest(volumeCm3: 150m);
        var config = CreateTestConfig(
            printSpeedCm3PerHour: 15m,
            machineHourlyRate: 500m);

        // Act
        var result = await _engine.CalculateAsync(request, config, CancellationToken.None);

        // Assert
        // Machine time = volume / speed * hourly rate
        var expectedPrintHours = 150m / 15m;
        var expectedTimeCost = expectedPrintHours * 500m;
        Assert.Equal(expectedTimeCost, result.MachineTimeCost, 2);
    }

    /// <summary>
    /// Verifies that complexity surcharge is applied when ratio exceeds threshold.
    /// </summary>
    [Theory]
    [InlineData(100, 100, 0)] // Ratio = 1.0, no surcharge
    [InlineData(100, 250, 0)] // Ratio = 2.5, surcharge applied
    [InlineData(50, 300, 0)] // Ratio = 6.0, surcharge applied
    public async Task Calculate_ComplexitySurcharge_AppliedCorrectly(
        decimal volumeCm3,
        decimal surfaceAreaCm2,
        decimal expectedMinSurcharge)
    {
        // Arrange
        var request = CreateTestRequest(volumeCm3, surfaceAreaCm2);
        var config = CreateTestConfig(complexityThreshold: 2.0m, complexitySurchargePercent: 15m);

        // Act
        var result = await _engine.CalculateAsync(request, config, CancellationToken.None);

        // Assert
        var ratio = surfaceAreaCm2 / volumeCm3;
        if (ratio > config.ComplexityThreshold)
        {
            Assert.True(result.ComplexitySurcharge > 0,
                $"Surcharge should be applied for ratio {ratio} > threshold {config.ComplexityThreshold}");
        }
        else
        {
            Assert.Equal(0m, result.ComplexitySurcharge);
        }
    }

    /// <summary>
    /// Verifies that margin is applied correctly.
    /// </summary>
    [Fact]
    public async Task Calculate_MarginMultiplier_AppliedCorrectly()
    {
        // Arrange
        var request = CreateTestRequest(volumeCm3: 100m);
        var config = CreateTestConfig(marginMultiplier: 1.5m);

        // Act
        var result = await _engine.CalculateAsync(request, config, CancellationToken.None);

        // Assert
        var expectedMargin = result.SubtotalBeforeMargin * 0.5m;
        Assert.Equal(expectedMargin, result.MarginAmount, 2);
        Assert.Equal(result.SubtotalBeforeMargin * 1.5m, result.TotalUnitPrice, 2);
    }

    /// <summary>
    /// Verifies that minimum order price is enforced.
    /// </summary>
    [Fact]
    public async Task Calculate_MinimumOrderPrice_Enforced()
    {
        // Arrange
        var request = CreateTestRequest(volumeCm3: 1m); // Very small part
        var config = CreateTestConfig(minimumOrderPrice: 100m);

        // Act
        var result = await _engine.CalculateAsync(request, config, CancellationToken.None);

        // Assert
        Assert.True(result.TotalPrice >= config.MinimumOrderPrice,
            $"Total price ({result.TotalPrice}) should be at least minimum ({config.MinimumOrderPrice})");
    }

    private static PricingRequest CreateTestRequest(
        decimal volumeCm3 = 100m,
        decimal surfaceAreaCm2 = 200m)
    {
        return new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "TEST",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "FDM",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = volumeCm3,
                SupportVolumeCm3 = volumeCm3 * 0.1m,
                SurfaceAreaCm2 = surfaceAreaCm2,
                BoundingBoxX = 10m,
                BoundingBoxY = 10m,
                BoundingBoxZ = 10m,
                IsManifold = true,
                TriangleCount = 5000
            }
        };
    }

    private static PricingConfiguration CreateTestConfig(
        decimal materialPricePerCm3 = 0.05m,
        decimal printSpeedCm3PerHour = 15m,
        decimal machineHourlyRate = 500m,
        decimal marginMultiplier = 1.5m,
        decimal complexityThreshold = 2.0m,
        decimal complexitySurchargePercent = 15m,
        decimal minimumOrderPrice = 100m)
    {
        return new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = materialPricePerCm3,
            SupportMaterialPricePerCm3 = materialPricePerCm3 * 0.6m,
            DensityGramPerCm3 = 1.24m,
            MachineHourlyRate = machineHourlyRate,
            PrintSpeedCm3PerHour = printSpeedCm3PerHour,
            SetupCostFlat = 50m,
            MinimumOrderPrice = minimumOrderPrice,
            MarginMultiplier = marginMultiplier,
            ComplexityThreshold = complexityThreshold,
            ComplexitySurchargePercent = complexitySurchargePercent,
            EffectiveFrom = DateTime.UtcNow.AddYears(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
    }
}
```

**Checklist**:
- [ ] Test factory created with Testcontainers (PostgreSQL 18, Redis 8.4)
- [ ] Controller integration tests created
- [ ] Pricing engine unit tests created
- [ ] Test data seeding implemented
- [ ] All tests passing with `dotnet test`

---

## Phase 10: GitOps & Deployment

### Task 10.1: Create Kubernetes Manifests

**File**: `maliev-gitops/3-apps/maliev-pricing-service/base/deployment.yaml`

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: maliev-pricing-service
  labels:
    app: maliev-pricing-service
    version: v1
spec:
  replicas: 2
  selector:
    matchLabels:
      app: maliev-pricing-service
  template:
    metadata:
      labels:
        app: maliev-pricing-service
        version: v1
    spec:
      serviceAccountName: maliev-pricing-service
      containers:
        - name: maliev-pricing-service
          image: asia-southeast1-docker.pkg.dev/maliev-website/maliev-website-artifact-dev/maliev-pricing-service:latest
          ports:
            - containerPort: 8080
              name: http
          envFrom:
            - secretRef:
                name: maliev-pricing-service-secrets
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: Services__MaterialService__BaseUrl
              value: "http://maliev-materialservice-api"
            - name: Services__CurrencyService__BaseUrl
              value: "http://maliev-currencyservice-api"
            - name: Services__IAM__BaseUrl
              value: "http://maliev-iamservice-api"
          resources:
            requests:
              cpu: "100m"
              memory: "256Mi"
            limits:
              cpu: "500m"
              memory: "512Mi"
          livenessProbe:
            httpGet:
              path: /pricing/liveness
              port: http
            initialDelaySeconds: 15
            periodSeconds: 10
            timeoutSeconds: 5
          readinessProbe:
            httpGet:
              path: /pricing/readiness
              port: http
            initialDelaySeconds: 5
            periodSeconds: 5
            timeoutSeconds: 3
          securityContext:
            runAsNonRoot: true
            readOnlyRootFilesystem: true
            allowPrivilegeEscalation: false
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
            - weight: 100
              podAffinityTerm:
                labelSelector:
                  matchLabels:
                    app: maliev-pricing-service
                topologyKey: kubernetes.io/hostname
```

**File**: `maliev-gitops/3-apps/maliev-pricing-service/base/service.yaml`

```yaml
apiVersion: v1
kind: Service
metadata:
  name: maliev-pricing-service-api
  labels:
    app: maliev-pricing-service
spec:
  type: ClusterIP
  ports:
    - port: 80
      targetPort: http
      protocol: TCP
      name: http
  selector:
    app: maliev-pricing-service
```

**File**: `maliev-gitops/3-apps/maliev-pricing-service/base/hpa.yaml`

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: maliev-pricing-service
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: maliev-pricing-service
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

**File**: `maliev-gitops/3-apps/maliev-pricing-service/base/external-secret.yaml`

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: maliev-pricing-service-secrets
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: gcp-secret-manager
    kind: ClusterSecretStore
  target:
    name: maliev-pricing-service-secrets
    creationPolicy: Owner
  data:
    - secretKey: ConnectionStrings__PricingDbContext
      remoteRef:
        key: pricing-db-connection-string
    - secretKey: ConnectionStrings__Cache
      remoteRef:
        key: redis-connection-string
    - secretKey: RabbitMQ__Host
      remoteRef:
        key: rabbitmq-host
    - secretKey: RabbitMQ__Username
      remoteRef:
        key: rabbitmq-username
    - secretKey: RabbitMQ__Password
      remoteRef:
        key: rabbitmq-password
    - secretKey: Jwt__SecretKey
      remoteRef:
        key: jwt-secret-key
```

**File**: `maliev-gitops/3-apps/maliev-pricing-service/base/kustomization.yaml`

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: maliev-dev

resources:
  - deployment.yaml
  - service.yaml
  - hpa.yaml
  - external-secret.yaml
  - serviceaccount.yaml

commonLabels:
  app.kubernetes.io/name: maliev-pricing-service
  app.kubernetes.io/part-of: maliev-platform
```

**File**: `maliev-gitops/3-apps/maliev-pricing-service/base/serviceaccount.yaml`

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: maliev-pricing-service
  annotations:
    iam.gke.io/gcp-service-account: maliev-pricing-service@maliev-website.iam.gserviceaccount.com
```

### Task 10.2: Create ArgoCD Application

**File**: `maliev-gitops/argocd/maliev-pricing-service.yaml`

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: maliev-pricing-service
  namespace: argocd
  finalizers:
    - resources-finalizer.argocd.argoproj.io
spec:
  project: maliev
  source:
    repoURL: https://github.com/MALIEV-Co-Ltd/maliev-gitops.git
    targetRevision: HEAD
    path: 3-apps/maliev-pricing-service/overlays/development
  destination:
    server: https://kubernetes.default.svc
    namespace: maliev-dev
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
      - PrunePropagationPolicy=foreground
    retry:
      limit: 5
      backoff:
        duration: 5s
        factor: 2
        maxDuration: 3m
```

### Task 10.3: Create CI/CD Workflows

**File**: `.github/workflows/ci-develop.yml`

```yaml
name: CI - Develop

on:
  push:
    branches: [develop]
  pull_request:
    branches: [develop]

env:
  REGISTRY: asia-southeast1-docker.pkg.dev
  PROJECT_ID: maliev-website
  REPOSITORY: maliev-website-artifact-dev
  IMAGE_NAME: maliev-pricing-service
  GITOPS_REPO: MALIEV-Co-Ltd/maliev-gitops

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore Maliev.PricingService.slnx
        env:
          NUGET_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Build
        run: dotnet build Maliev.PricingService.slnx --no-restore --configuration Release

      - name: Test
        run: dotnet test Maliev.PricingService.slnx --no-build --configuration Release --verbosity normal

  build-and-push:
    needs: build-and-test
    if: github.event_name == 'push'
    runs-on: ubuntu-latest
    outputs:
      image_tag: ${{ steps.meta.outputs.tags }}
    steps:
      - uses: actions/checkout@v4

      - name: Authenticate to Google Cloud
        uses: google-github-actions/auth@v2
        with:
          credentials_json: ${{ secrets.GCP_CREDENTIALS }}

      - name: Set up Cloud SDK
        uses: google-github-actions/setup-gcloud@v2

      - name: Configure Docker
        run: gcloud auth configure-docker ${{ env.REGISTRY }}

      - name: Build and push Docker image
        run: |
          docker build \
            --build-arg GITHUB_TOKEN=${{ secrets.GITHUB_TOKEN }} \
            -t ${{ env.REGISTRY }}/${{ env.PROJECT_ID }}/${{ env.REPOSITORY }}/${{ env.IMAGE_NAME }}:${{ github.sha }} \
            -f Maliev.PricingService.Api/Dockerfile .
          docker push ${{ env.REGISTRY }}/${{ env.PROJECT_ID }}/${{ env.REPOSITORY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}

  update-gitops:
    needs: build-and-push
    if: github.event_name == 'push'
    runs-on: ubuntu-latest
    steps:
      - name: Checkout GitOps repo
        uses: actions/checkout@v4
        with:
          repository: ${{ env.GITOPS_REPO }}
          token: ${{ secrets.GITOPS_PAT }}

      - name: Update image tag
        run: |
          cd 3-apps/maliev-pricing-service/overlays/development
          kustomize edit set image \
            ${{ env.REGISTRY }}/${{ env.PROJECT_ID }}/${{ env.REPOSITORY }}/${{ env.IMAGE_NAME }}=${{ env.REGISTRY }}/${{ env.PROJECT_ID }}/${{ env.REPOSITORY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}

      - name: Commit and push
        run: |
          git config user.name "GitHub Actions"
          git config user.email "actions@github.com"
          git add .
          git commit -m "chore: update maliev-pricing-service to ${{ github.sha }}"
          git push
```

**Checklist**:
- [ ] Deployment manifest created
- [ ] Service manifest created
- [ ] HPA manifest created
- [ ] ExternalSecret manifest created
- [ ] ServiceAccount manifest created
- [ ] Kustomization file created
- [ ] ArgoCD Application created
- [ ] CI/CD workflow for develop branch created
- [ ] CI/CD workflow for staging branch created
- [ ] CI/CD workflow for main branch created

---

## Phase 11: Aspire Orchestration

> **Status**: COMPLETED
> **Purpose**: Configure PricingService for local development with .NET Aspire

### Overview

The Maliev platform uses .NET Aspire for local development orchestration. PricingService must be registered in the Aspire AppHost to:
- Automatically provision its PostgreSQL database
- Connect to shared RabbitMQ and Redis infrastructure
- Reference MaterialService and CurrencyService for pricing lookups
- Register IAM permissions on startup

### Files Modified

#### `Maliev.Aspire.AppHost/AppHost.cs`

**1. Add database to ConfigureDatabases() method:**

```csharp
public static ServiceDatabases ConfigureDatabases(IResourceBuilder<PostgresServerResource> postgres)
{
    return new ServiceDatabases(
        // ... existing databases ...
        Pricing: postgres.AddDatabase("pricing-app-db"),
        // ... remaining databases ...
    );
}
```

**2. Add property to ServiceDatabases record:**

```csharp
record ServiceDatabases(
    // ... existing properties ...
    IResourceBuilder<PostgresDatabaseResource> Pricing,
    // ... remaining properties ...
);
```

**3. Add service registration in ConfigureServices() method (after materialService):**

```csharp
var pricingService = WithSharedSecrets(
    builder.AddProject<Projects.Maliev_PricingService_Api>("maliev-pricingservice-api")
        .WithReference(databases.Pricing, "PricingDbContext")
        .WaitFor(databases.Pricing)
        .WithReference(infrastructure.RabbitMQ)
        .WaitFor(infrastructure.RabbitMQ)
        .WithReference(infrastructure.Redis)
        .WithReference(materialService)
        .WithReference(currencyService)
        .WithReference(iamService)
        .WithHttpHealthCheck("/pricing/aspire-liveness"),
    config,
    grafana,
    otelCollector);
```

#### `Maliev.Aspire.AppHost/Maliev.Aspire.AppHost.csproj`

**Add project reference:**

```xml
<ProjectReference Include="..\..\Maliev.PricingService\Maliev.PricingService.Api\Maliev.PricingService.Api.csproj" />
```

### Service Dependencies in Aspire

```
PricingService
├── databases.Pricing (PostgreSQL: pricing-app-db)
├── infrastructure.RabbitMQ (message bus)
├── infrastructure.Redis (distributed cache)
├── materialService (lookup material pricing)
├── currencyService (currency conversion)
└── iamService (permission registration)
```

### Running Locally with Aspire

```bash
cd B:\maliev\Maliev.Aspire\Maliev.Aspire.AppHost
dotnet run
```

The Aspire dashboard will be available at `https://localhost:15046` (or similar port shown in console output).

**PricingService endpoints in Aspire:**
- API: `http://localhost:{port}/pricing/v1/`
- Scalar docs: `http://localhost:{port}/pricing/scalar`
- Health: `http://localhost:{port}/pricing/aspire-liveness`

### Verifying Aspire Configuration

1. Run Aspire AppHost
2. Open Aspire dashboard
3. Verify `maliev-pricingservice-api` appears in the service list
4. Check that database `pricing-app-db` is created
5. Verify health check passes (green status)
6. Test endpoint via Scalar documentation

**Checklist**:
- [x] Database added to ConfigureDatabases()
- [x] Property added to ServiceDatabases record
- [x] Service registered in ConfigureServices()
- [x] Project reference added to .csproj
- [ ] Service starts successfully in Aspire
- [ ] Health check passes
- [ ] Can access Scalar documentation

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
