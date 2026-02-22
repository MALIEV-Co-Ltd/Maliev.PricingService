# Implementation Plan: Pricing Service Integration (.NET 10)

## Technical Context
- **Language**: C# 14 / .NET 10.0
- **Framework**: ASP.NET Core Web API
- **Data Access**: Entity Framework Core 10 (PostgreSQL)
- **Messaging**: MassTransit with **RabbitMQ** (No other brokers)
- **Contracts**: Shared events stored in `Maliev.MessagingContracts` (Central Repository)
- **Service Standard**: Integrated with `Maliev.Aspire.ServiceDefaults`
- **Dependencies**: 
  - Local: `ProjectReference` for `ServiceDefaults` and `MessagingContracts`
  - CI: Automated switch to `PackageReference` (handled by MSBuild conditions)
- **Forbidden**: No FluentAssertions, No FluentValidations, No AutoMapper. Use standard System.ComponentModel.DataAnnotations and manual mapping for performance/clarity.

## Constitution Check
- **Identity**: GCP-style Service Account integration (Compliant)
- **Resiliency**: Built-in retry/circuit breakers via MassTransit/RabbitMQ (Compliant)
- **Quality**: Adheres to MALIEV .NET 10 standards (Compliant)

## Phase 0: Setup & Infrastructure
1. Create Solution: `Maliev.PricingService.sln`.
2. Project `Maliev.PricingService.Api` (Web API).
3. Project `Maliev.PricingService.Data` (Class Library).
4. Project `Maliev.PricingService.Tests` (xUnit).
5. **Workflow Integration**: Copy `ci-*.yml` and `gemini-*.yml` from `Maliev.QuotationService`.
6. **Access Control**: Create `CODEOWNERS` file.
7. **Documentation**: Initialize `README.md` following standard format.

## Phase 1: Foundation & Data
1. Implement `PricingDbContext` with PostgreSQL 18.
2. Define `PricingConfiguration` and `PricingAuditRecord` entities.
3. **Remediation**: Implement Historical Data Migration service for legacy PostgreSQL import.

## Phase 2: Pricing Engines & Audit
1. Implement `RuleBasedPricingEngine` (Deterministic).
2. Implement `PricingOrchestrator`:
   - Validates context (Customer/Product).
   - Executes engine.
   - Persists immutable `PricingAuditRecord`.
   - Integrates Loyalty/Discount logic from `PricingConfiguration`.

## Phase 3: MassTransit & RabbitMQ
1. Update `Maliev.MessagingContracts` with `FileAnalyzedEvent` and `PriceCalculatedEvent`.
2. Implement `FileAnalyzedEventConsumer`:
   - Triggered by `GeometryService`.
   - Resulting price published as `PriceCalculatedEvent`.
3. Configure RabbitMQ with `ServiceDefaults`.

## Phase 4: Verification
1. Add Linting/Format check tasks.
2. Integration tests for event-to-audit-to-event flow.
