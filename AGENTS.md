# Maliev Pricing Service - Agentic Coding Guide

This document provides instructions and guidelines for AI agents working on the Maliev Pricing Service codebase.

## 1. Build and Test Commands

**Prerequisites:**
- .NET SDK (targeting `net10.0`)
- Docker (required for integration tests via `Testcontainers`)

### Core Commands
| Action | Command | Notes |
|--------|---------|-------|
| **Build** | `dotnet build` | Treats warnings as errors. |
| **Test (All)** | `dotnet test` | Runs all unit and integration tests. |
| **Run API** | `dotnet run --project Maliev.PricingService/Maliev.PricingService.Api` | Starts the API service. |
| **Format** | `dotnet format` | Enforces code style. |

### Running Specific Tests
To run a single test or a subset of tests, use the `--filter` option.

**Examples:**
- Run a specific test method:
  ```bash
  dotnet test --filter "FullyQualifiedName=Maliev.PricingService.Tests.Unit.PricingOrchestratorTests.CalculatePriceAsync_ValidRequest_ReturnsResult"
  ```
- Run all tests in a class:
  ```bash
  dotnet test --filter "FullyQualifiedName~Maliev.PricingService.Tests.Unit.PricingOrchestratorTests"
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

Follow standard C# coding conventions and the existing patterns in the codebase.

### General
- **Namespaces**: Use file-scoped namespaces (e.g., `namespace Maliev.PricingService.Api.Controllers;`).
- **Formatting**: PascalCase for public members/types, camelCase for parameters/locals.
- **Async/Await**: Use `async/await` for all I/O-bound operations. Pass `CancellationToken` through to async methods.
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Handle potential nulls explicitly.

### Classes & Dependency Injection
- Use `private readonly` fields for dependencies.
- Inject dependencies via constructor.
- Underscore prefix for private fields (e.g., `_orchestrator`).

```csharp
public class PricingService
{
    private readonly IPricingRepository _repository;

    public PricingService(IPricingRepository repository)
    {
        _repository = repository;
    }
}
```

### Logging
- Use structured logging with `ILogger<T>`.
- Do not interpolate strings in log messages; use placeholders.

```csharp
// Correct
_logger.LogInformation("Processing pricing for FileId: {FileId}", request.FileId);

// Incorrect
_logger.LogInformation($"Processing pricing for FileId: {request.FileId}");
```

### API Controllers
- Decorate with `[ApiController]`, `[ApiVersion]`, and `[Route]`.
- Return `ActionResult<T>`.
- Use `ProducesResponseType` to document status codes.
- Validate `ModelState` if necessary (though `[ApiController]` handles mostly automatically).

### Error Handling
- Use `try-catch` blocks in higher-level components (Controllers/Consumers) to catch and log exceptions.
- Return appropriate HTTP status codes (e.g., 400 for bad input, 500 for internal errors).
- Use `ProblemDetails` format (implicit in `BadRequest(ModelState)` or similar).

## 4. Testing Guidelines

- **Unit Tests**: Test logic in isolation. Mock dependencies using `Moq`.
- **Integration Tests**: Use `Testcontainers` for real infrastructure dependencies (Postgres, RabbitMQ, Redis).
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `CalculatePrice_InvalidInput_ThrowsException`).

## 5. Agent Instructions

1.  **Verification**: ALWAYS run `dotnet build` after making changes to ensure no compilation errors.
2.  **Testing**: If modifying logic, run relevant existing tests. If adding features, add corresponding tests.
3.  **Context**: Read `Directory.Build.props` and `.csproj` files to understand dependencies and build configurations before adding new packages.
4.  **No Assumptions**: Do not assume global tools are installed. Use local `dotnet` CLI commands.
