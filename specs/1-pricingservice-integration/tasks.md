# Implementation Tasks: Pricing Service Integration (.NET 10)

## Phase 1: Setup & Project Scaffolding
- [X] T001 Create .NET 10 solution and projects (Api, Data, Tests)
- [X] T002 Update `Maliev.MessagingContracts` with `FileAnalyzedEvent` and `PriceCalculatedEvent`
- [X] T007 Initialize `README.md` following standard format (Completed)


## Phase 2: Foundational Data Layer
- [X] T008 Implement `PricingDbContext` and entities (`PricingConfiguration`, `PricingAuditRecord`)
- [X] T009 Create `HistoricalMigrationService` to import legacy pricing data
- [X] T010 Implement PostgreSQL migrations and initial seeding

## Phase 3: Pricing Orchestration (US1 & US2)
- [X] T011 [US1] Implement `IPricingEngine` and `RuleBasedPricingEngine`
- [X] T012 [US1] Implement `PricingOrchestrator` with manual mapping (No AutoMapper)
- [X] T013 [US2] Add Loyalty Tier lookup and discount calculation to `PricingOrchestrator`
- [X] T014 [P] Implement `PricingController` for ad-hoc requests via `POST /v1/pricing/calculate`

## Phase 4: Messaging & Integration (US2 & US3)
- [X] T015 [US2] Implement `FileAnalyzedEventConsumer` using MassTransit
- [X] T016 [US2] Configure MassTransit to use **RabbitMQ** only via `ServiceDefaults` extensions
- [X] T017 [US3] Implement stale-price fallback in `PricingOrchestrator` using `IMemoryCache`

## Phase 5: Verification & Quality (C1)
- [X] T018 **[Remediation C1]** Setup `.editorconfig` and add task for `dotnet format --verify-no-changes`
- [X] T019 **[Remediation C1]** Add `dotnet build` and `dotnet test` validation to project root
- [X] T020 Implement integration tests for `FileAnalyzedEvent` -> `PriceCalculatedEvent` flow

## Dependencies
Phase 1 → Phase 2
Phase 2 → Phase 3
US1 → US2 (Engine logic required for event responses)
