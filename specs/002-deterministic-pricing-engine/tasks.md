# Tasks: Deterministic Pricing Engine

**Input**: Design documents from `/specs/002-deterministic-pricing-engine/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are included in the Polish phase (Phase 7) as the spec includes explicit test requirements.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Web API microservice**: `Maliev.PricingService.Api/`, `Maliev.PricingService.Data/`, `Maliev.PricingService.Tests/`
- Existing project structure preserved

---

## Phase 1: Setup

**Purpose**: Verify dependencies and create directory structure

- [x] T001 Verify MaterialService has Density, CostPerKg, and ProcessParameters fields available
- [x] T002 Create Calculators directory at Maliev.PricingService.Api/Services/Calculators/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Remove ML engine and establish core models that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### ML Engine Removal

- [x] T003 Delete Maliev.PricingService.Api/Services/IMLPricingEngine.cs
- [x] T004 Delete Maliev.PricingService.Api/Services/MLPricingEngine.cs
- [x] T005 Remove IMLPricingEngine registration from Maliev.PricingService.Api/Program.cs
- [x] T006 Remove any `<PackageReference Include="Microsoft.ML.*" />` entries from Maliev.PricingService.Api/Maliev.PricingService.Api.csproj
- [x] T007 Remove MLEnhanced=2 and Hybrid=4 from PricingStrategy enum in Maliev.PricingService.Api/Services/PricingModels.cs
- [x] T008 Remove MLEnhanced=2 and Hybrid=4 from PricingStrategy enum in Maliev.PricingService.Data/Entities/PricingAuditRecord.cs
- [x] T009 Search codebase for "PrintingTechnology" references and rename to "ManufacturingTechnology" (FR-016)

### New Models

- [x] T010 Add ManufacturingTechnology enum (Fdm=1, Sla=2, Cnc=3, Scanning=4, Design=5) to Maliev.PricingService.Api/Services/PricingModels.cs
- [x] T011 Add MaterialData record to Maliev.PricingService.Api/Services/PricingModels.cs
- [x] T012 Add MachineRates record to Maliev.PricingService.Api/Services/PricingModels.cs
- [x] T013 Add Technology, LayerHeightMm, SupportEnabled, HeightMm, ScanningTier fields to PricingRequest in Maliev.PricingService.Api/Services/PricingModels.cs
- [x] T014 Add Notes field to PricingResult in Maliev.PricingService.Api/Services/PricingModels.cs
- [x] T015 Rename PricePerKg to CostPerKg in MaterialDto in Maliev.PricingService.Api/Clients/IMaterialServiceClient.cs
- [x] T016 Add ProcessParameters dictionary to MaterialDto in Maliev.PricingService.Api/Clients/IMaterialServiceClient.cs

### Calculator Interface

- [x] T017 Create IPricingCalculator interface in Maliev.PricingService.Api/Services/Calculators/IPricingCalculator.cs

### Configuration

- [x] T018 Add Pricing:MachineRates section to Maliev.PricingService.Api/appsettings.json
- [x] T019 Register IOptions<MachineRates> in Maliev.PricingService.Api/Program.cs

### Build Verification

- [x] T020 Run `dotnet build` and fix any compilation errors from removed types

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Stories 1 & 2 Combined - Deterministic Technology-Specific Pricing (Priority: P1) 🎯 MVP

**Goal**: Replace ML engine with deterministic calculators for all 5 technologies. Customers receive identical prices for identical inputs, with technology-specific pricing logic.

**Why Combined**: US1 (determinism) and US2 (technology-specific) are tightly coupled - US1 acceptance scenarios already test FDM/SLA/CNC floors which require US2 calculators.

**Independent Test**: Submit identical quote requests multiple times and verify identical prices; submit with different technologies and verify distinct, appropriate pricing.

### Calculator Implementations

- [x] T021 [P] [US1+US2] Implement FdmPricingCalculator in Maliev.PricingService.Api/Services/Calculators/FdmPricingCalculator.cs (minimum 300 THB)
- [x] T022 [P] [US1+US2] Implement SlaPricingCalculator in Maliev.PricingService.Api/Services/Calculators/SlaPricingCalculator.cs (minimum 500 THB)
- [x] T023 [P] [US1+US2] Implement CncPricingCalculator in Maliev.PricingService.Api/Services/Calculators/CncPricingCalculator.cs (minimum 2500 THB)
- [x] T024 [P] [US1+US2] Implement ScanningPricingCalculator in Maliev.PricingService.Api/Services/Calculators/ScanningPricingCalculator.cs (2500/4500 THB tiers)
- [x] T025 [P] [US1+US2] Implement DesignPricingCalculator in Maliev.PricingService.Api/Services/Calculators/DesignPricingCalculator.cs (500 THB minimum)

### Engine Refactor

- [x] T026 [US1+US2] Update IPricingEngine.CalculateAsync signature to accept MaterialData and MachineRates in Maliev.PricingService.Api/Services/IPricingEngine.cs
- [x] T027 [US1+US2] Inject IEnumerable<IPricingCalculator> and IOptions<MachineRates> into RuleBasedPricingEngine constructor in Maliev.PricingService.Api/Services/RuleBasedPricingEngine.cs
- [x] T028 [US1+US2] Refactor RuleBasedPricingEngine.CalculateAsync to select calculator by Technology and delegate calculation in Maliev.PricingService.Api/Services/RuleBasedPricingEngine.cs
- [x] T029 [US1+US2] Register all 5 calculators in DI container in Maliev.PricingService.Api/Program.cs

### Orchestrator Update

- [x] T030 [US1+US2] Inject IMaterialServiceClient into PricingOrchestrator in Maliev.PricingService.Api/Services/PricingOrchestrator.cs
- [x] T031 [US1+US2] Inject IOptions<MachineRates> into PricingOrchestrator in Maliev.PricingService.Api/Services/PricingOrchestrator.cs
- [x] T032 [US1+US2] Remove PricingConfiguration DB query from PricingOrchestrator.CalculatePriceAsync in Maliev.PricingService.Api/Services/PricingOrchestrator.cs
- [x] T033 [US1+US2] Add MaterialService call to fetch MaterialData in PricingOrchestrator in Maliev.PricingService.Api/Services/PricingOrchestrator.cs
- [x] T034 [US1+US2] Map MaterialDto to MaterialData with ProcessParameters extraction in Maliev.PricingService.Api/Services/PricingOrchestrator.cs

### Build Verification

- [x] T035 Run `dotnet build` and verify zero errors

**Checkpoint**: At this point, US1+US2 should be fully functional - deterministic pricing for all 5 technologies

---

## Phase 4: User Story 3 - File Upload Auto-Estimate (Priority: P2)

**Goal**: Generate automatic FDM-based preliminary estimate when customer uploads a 3D model file.

**Independent Test**: Upload a 3D model file and verify an FDM-based estimate is automatically generated using default parameters (0.2mm layer height, no supports).

- [x] T036 [US3] Add Technology=ManufacturingTechnology.Fdm default to FileAnalyzedEventConsumer in Maliev.PricingService.Api/Consumers/FileAnalyzedEventConsumer.cs
- [x] T037 [US3] Add LayerHeightMm=0.2m default to FileAnalyzedEventConsumer in Maliev.PricingService.Api/Consumers/FileAnalyzedEventConsumer.cs
- [x] T038 [US3] Add SupportEnabled=false default to FileAnalyzedEventConsumer in Maliev.PricingService.Api/Consumers/FileAnalyzedEventConsumer.cs
- [x] T039 [US3] Add HeightMm=message.BoundingBoxZ mapping to FileAnalyzedEventConsumer in Maliev.PricingService.Api/Consumers/FileAnalyzedEventConsumer.cs

**Checkpoint**: US3 complete - file uploads generate automatic FDM estimates

---

## Phase 5: User Story 4 - Material Properties Integration (Priority: P2)

**Goal**: Pricing reflects material's density, cost, and processing characteristics from MaterialService.

**Independent Test**: Select different materials for the same part and verify prices vary according to material properties.

- [x] T040 [US4] Update MaterialServiceClient to map CostPerKg from API response in Maliev.PricingService.Api/Clients/MaterialServiceClient.cs
- [x] T041 [US4] Update MaterialServiceClient to map ProcessParameters dictionary from API response in Maliev.PricingService.Api/Clients/MaterialServiceClient.cs
- [x] T042 [US4] Add error handling for missing material properties in Maliev.PricingService.Api/Services/PricingOrchestrator.cs

**Checkpoint**: US4 complete - material properties correctly integrated

---

## Phase 6: User Story 5 - Audit Records (Priority: P3)

**Goal**: All pricing calculations logged for audit purposes with technology field.

**Independent Test**: Generate quotes and verify audit records are persisted with technology, inputs, and outputs.

- [x] T043 [US5] Add Technology string field to PricingAuditRecord entity in Maliev.PricingService.Data/Entities/PricingAuditRecord.cs
- [x] T044 [US5] Remove MLModelVersion field from PricingAuditRecord entity in Maliev.PricingService.Data/Entities/PricingAuditRecord.cs
- [x] T045 [US5] Remove ConfigMaterialPricePerCm3 field from PricingAuditRecord entity in Maliev.PricingService.Data/Entities/PricingAuditRecord.cs
- [x] T046 [US5] Remove ConfigSupportPricePerCm3 field from PricingAuditRecord entity in Maliev.PricingService.Data/Entities/PricingAuditRecord.cs
- [x] T047 [US5] Make PricingConfigurationId nullable in PricingAuditRecord entity in Maliev.PricingService.Data/Entities/PricingAuditRecord.cs
- [x] T048 [US5] Update audit record creation in PricingOrchestrator to set Technology field in Maliev.PricingService.Api/Services/PricingOrchestrator.cs
- [x] T049 [US5] Remove MLModelVersion assignment from audit record in PricingOrchestrator in Maliev.PricingService.Api/Services/PricingOrchestrator.cs
- [x] T050 [US5] Create EF Core migration for PricingAuditRecord schema changes

**Checkpoint**: US5 complete - audit records persist with new fields

---

## Phase 7: Polish & Testing

**Purpose**: Unit tests, integration tests, performance validation, and cleanup

### Unit Tests (from spec test requirements)

- [x] T051 [P] Create unit test for FDM calculator (10×10×10mm cube, PLA) in Maliev.PricingService.Tests/Unit/Calculators/FdmPricingCalculatorTests.cs
- [x] T052 [P] Create unit test for FDM hollow basket scenario (MinLayerTime logic) in Maliev.PricingService.Tests/Unit/Calculators/FdmPricingCalculatorTests.cs
- [x] T053 [P] Create unit test for SLA calculator (minimum 500 THB) in Maliev.PricingService.Tests/Unit/Calculators/SlaPricingCalculatorTests.cs
- [x] T054 [P] Create unit test for CNC calculator (minimum 2500 THB) in Maliev.PricingService.Tests/Unit/Calculators/CncPricingCalculatorTests.cs
- [x] T055 [P] Create unit test for Scanning calculator (both tiers) in Maliev.PricingService.Tests/Unit/Calculators/ScanningPricingCalculatorTests.cs
- [x] T056 [P] Create unit test for Design calculator (500 THB minimum) in Maliev.PricingService.Tests/Unit/Calculators/DesignPricingCalculatorTests.cs

### Determinism Test (SC-001)

- [x] T057 Create determinism test verifying identical inputs produce identical outputs (call calculator twice with same PricingRequest, MaterialData, MachineRates; assert all result fields match exactly) in Maliev.PricingService.Tests/Unit/Calculators/DeterminismTests.cs

### Performance Test (SC-007)

- [x] T058 Create performance benchmark test verifying calculations complete under 1 second for typical requests in Maliev.PricingService.Tests/Unit/Calculators/PerformanceTests.cs

### Integration Test

- [x] T059 Create integration test with mocked MaterialServiceClient verifying end-to-end pricing flow in Maliev.PricingService.Tests/Integration/PricingIntegrationTests.cs

### Final Validation

- [x] T060 Run `dotnet test` and verify all tests pass
- [x] T061 Run `dotnet build` and verify zero errors
- [x] T062 Verify no ML.net references remain (`grep -r "Microsoft.ML" .` returns empty)
- [x] T063 Run quickstart.md validation scenarios

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **US1+US2 (Phase 3)**: Depends on Foundational phase completion
- **US3 (Phase 4)**: Depends on US1+US2 completion (needs FDM calculator working)
- **US4 (Phase 5)**: Depends on US1+US2 completion (needs MaterialData mapping)
- **US5 (Phase 6)**: Depends on US1+US2 completion (needs Technology field populated)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1+US2 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **US3 (P2)**: Depends on US1+US2 - needs FDM calculator functional
- **US4 (P2)**: Depends on US1+US2 - needs MaterialData integration
- **US5 (P3)**: Depends on US1+US2 - needs Technology field in pricing flow

### Within Each Phase

- Tasks marked [P] within same phase can run in parallel (different files)
- Calculator implementations (T021-T025) can all run in parallel
- Unit tests (T051-T056) can all run in parallel

### Parallel Opportunities

- All calculator implementations (T021-T025) can run in parallel
- All unit tests (T051-T056) can run in parallel
- US3, US4, US5 can potentially run in parallel after US1+US2 completes (if team capacity allows)

---

## Parallel Example: Phase 3 (US1+US2)

```bash
# Launch all calculators in parallel:
Task: "Implement FdmPricingCalculator in Maliev.PricingService.Api/Services/Calculators/FdmPricingCalculator.cs"
Task: "Implement SlaPricingCalculator in Maliev.PricingService.Api/Services/Calculators/SlaPricingCalculator.cs"
Task: "Implement CncPricingCalculator in Maliev.PricingService.Api/Services/Calculators/CncPricingCalculator.cs"
Task: "Implement ScanningPricingCalculator in Maliev.PricingService.Api/Services/Calculators/ScanningPricingCalculator.cs"
Task: "Implement DesignPricingCalculator in Maliev.PricingService.Api/Services/Calculators/DesignPricingCalculator.cs"
```

---

## Implementation Strategy

### MVP First (US1+US2 Only)

1. Complete Phase 1: Setup (verify dependencies)
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: US1+US2 (deterministic technology-specific pricing)
4. **STOP and VALIDATE**: Test determinism and all 5 technologies
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add US1+US2 → Test independently → Deploy/Demo (MVP!)
3. Add US3 → File upload estimates work → Deploy/Demo
4. Add US4 → Material integration complete → Deploy/Demo
5. Add US5 → Audit records complete → Deploy/Demo
6. Add Polish → Full test coverage → Deploy

---

## Summary

| Metric | Value |
|--------|-------|
| Total Tasks | 63 |
| Setup Tasks | 2 |
| Foundational Tasks | 18 |
| US1+US2 Tasks | 15 |
| US3 Tasks | 4 |
| US4 Tasks | 3 |
| US5 Tasks | 8 |
| Polish Tasks | 13 |
| Parallel Opportunities | 18 tasks marked [P] |

---

## Notes

- [P] tasks = different files, no dependencies within phase
- [Story] label maps task to specific user story for traceability
- US1 and US2 combined as they are tightly coupled (both P1)
- Tests included in Phase 7 per spec test requirements
- T006 added for FR-002 (remove ML.net packages from .csproj)
- T009 added for FR-016 (rename PrintingTechnology → ManufacturingTechnology)
- T057 determinism test specifies exact verification criteria
- T058 performance test added for SC-007 (sub-second calculation)
- T059 integration test added per plan.md Phase F
- Verify MaterialService dependency before starting (T001)
- Commit after each task or logical group
- Stop at any checkpoint to validate independently
