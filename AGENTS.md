# Maliev Pricing Service - Agentic Coding Guide

This document provides instructions and guidelines for AI agents working on the Maliev Pricing Service codebase.

## 1. Build, Test & Lint Commands

All commands run from within this service directory (`B:\maliev\Maliev.PricingService`).

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.PricingService.slnx

# Run all tests
dotnet test Maliev.PricingService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~PricingOrchestratorTests.CalculatePriceAsync_ValidRequest_ReturnsResult"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~PricingOrchestratorTests"

# Run with code coverage
dotnet test Maliev.PricingService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.PricingService.slnx

# EF Core migrations (Infrastructure project only)
dotnet ef migrations add <Name> --project Maliev.PricingService.Infrastructure --startup-project Maliev.PricingService.Infrastructure
```

## 2. Project Structure & Architecture

**Architecture**: Clean Architecture (Api, Application, Domain, Infrastructure, Tests)

- **`Maliev.PricingService.Api`**: Controllers, Consumers, Middleware
- **`Maliev.PricingService.Application`**: Use cases, handlers, DTOs, Pricing strategies
- **`Maliev.PricingService.Domain`**: Entities, interfaces, pricing rules
- **`Maliev.PricingService.Infrastructure`**: EF Core, repositories, external services
- **`Maliev.PricingService.Tests`**: Unit and Integration tests. Uses `xUnit` and `Testcontainers`.
- **`Maliev.MessagingContracts`**: Shared message contracts for MassTransit.

### Key Patterns
- **Orchestration**: Controllers delegate to `IPricingOrchestrator`, which manages workflow between engines/services.
- **Pricing Strategies**: FDM, SLA/DLP, CNC calculators as strategy implementations
- **Messaging**: Uses MassTransit with RabbitMQ. Consumers handle events (e.g., `FileAnalyzedEvent`).
- **Data Access**: EF Core with PostgreSQL.

## 3. Code Style & Conventions

### Workspace Structure
```
Maliev.PricingService/
├── Maliev.PricingService.Api/           # Controllers, Consumers, Middleware
├── Maliev.PricingService.Application/   # Use cases, DTOs, Interfaces, Handlers
├── Maliev.PricingService.Domain/        # Entities, value objects, domain interfaces
├── Maliev.PricingService.Infrastructure/ # EF Core DbContext, repositories, HTTP clients
├── Maliev.PricingService.Tests/         # Unit + Integration tests (xUnit)
├── Directory.Build.props                # Central package versioning
└── Maliev.PricingService.slnx          # Solution file (.slnx preferred over .sln)
```

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.PricingService.Api.Controllers;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `CalculatePriceAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IPricingOrchestrator`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `pricing.pricerequests.create`, `pricing.quotations.approve`
  - Invalid: `pricing.request.create` (singular), `pricing.create` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("pricing/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing pricing for FileId: {FileId}", fileId)`
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned

## 4. Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/pricing/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

## 5. Testing Rules

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

## 6. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("pricing.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (`/pricing`)
- **Scalar docs**: Configured at `/pricing/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
  - Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
  - Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
  - Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

## 7. Agent Instructions

1. **Verification**: ALWAYS run `dotnet build Maliev.PricingService.slnx` after making changes to ensure no compilation errors.
2. **Testing**: If modifying logic, run relevant existing tests. If adding features, add corresponding tests.
3. **Context**: Read `Directory.Build.props` and `.csproj` files to understand dependencies and build configurations before adding new packages.
4. **No Assumptions**: Do not assume global tools are installed. Use local `dotnet` CLI commands.

## Git Rules

- Each `Maliev.*` folder is an independent git repo. Work within this directory for git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked
