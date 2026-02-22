# Feature Specification: Deterministic Pricing Engine

**Feature Branch**: `002-deterministic-pricing-engine`  
**Created**: 2026-02-22  
**Status**: Draft  
**Input**: User description: "Deterministic Pricing Engine - Replace ML-based pricing with technology-specific calculators for Maliev.PricingService"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Customer Receives Consistent Quote (Priority: P1)

As a customer using the quote system at quote.maliev.com, I want to receive the same price every time I submit identical job specifications, so that I can trust the pricing system and make informed purchasing decisions.

**Why this priority**: This is the core value proposition - customers have lost trust due to inconsistent ML-based pricing (60-70% accuracy). Without consistent pricing, the entire quoting system fails to serve its purpose.

**Independent Test**: Can be fully tested by submitting the same quote request multiple times and verifying identical prices are returned each time. Delivers immediate trust restoration for customers.

**Acceptance Scenarios**:

1. **Given** a customer submits a quote request with specific parameters (material, geometry, technology), **When** they submit the same request again at any time, **Then** the returned price must be identical to the penny
2. **Given** a customer submits a quote for an FDM-printed part, **When** the price is calculated, **Then** the price must be at least 300 THB (minimum floor)
3. **Given** a customer submits a quote for an SLA-printed part, **When** the price is calculated, **Then** the price must be at least 500 THB (minimum floor)
4. **Given** a customer submits a quote for a CNC-machined part, **When** the price is calculated, **Then** the price must be at least 2,500 THB (minimum floor)

---

### User Story 2 - Quote Includes Technology-Specific Costs (Priority: P1)

As a customer, I want pricing that accurately reflects the manufacturing technology I choose (FDM, SLA, CNC, Scanning, or Design), so that I pay a fair price based on the actual production method.

**Why this priority**: Technology-specific pricing is fundamental to accurate quotes. Different manufacturing methods have vastly different costs (machine time, materials, labor), and customers need pricing that reflects their actual choice.

**Independent Test**: Can be tested by submitting identical part specifications with different technology selections and verifying each technology produces distinct, appropriate pricing.

**Acceptance Scenarios**:

1. **Given** a customer selects FDM technology for a part, **When** the price is calculated, **Then** the system accounts for layer height, support structures, material volume, and print time
2. **Given** a customer selects SLA technology for a part, **When** the price is calculated, **Then** the system accounts for layer exposure time, lift time, and resin material
3. **Given** a customer selects CNC technology for a part, **When** the price is calculated, **Then** the system accounts for material block size, removal volume, machinability, and complexity
4. **Given** a customer selects 3D Scanning service, **When** the price is calculated, **Then** the system returns a fixed minimum estimate with a note that final price is confirmed after inspection
5. **Given** a customer selects 3D Design service, **When** the price is calculated, **Then** the system returns a fixed minimum estimate with a note that final price is confirmed after scope discussion

---

### User Story 3 - File Upload Generates Initial Estimate (Priority: P2)

As a customer uploading a 3D model file, I want to receive an automatic preliminary price estimate, so that I can quickly understand approximate costs before configuring my exact requirements.

**Why this priority**: This provides a smooth user experience by giving immediate feedback. However, the estimate is just a starting point - customers will refine their technology choices later.

**Independent Test**: Can be tested by uploading a 3D model file and verifying an FDM-based estimate is automatically generated using default parameters.

**Acceptance Scenarios**:

1. **Given** a customer uploads a 3D model file with geometry metrics, **When** the file analysis completes, **Then** the system generates an initial FDM-based price estimate
2. **Given** an auto-generated estimate, **When** the customer later selects a different technology, **Then** the price is recalculated using their specified technology

---

### User Story 4 - Pricing Reflects Material Properties (Priority: P2)

As a customer selecting different materials, I want pricing to reflect the material's density, cost, and processing characteristics, so that I can compare material costs accurately.

**Why this priority**: Material selection significantly impacts price, but the pricing logic already accounts for this. This validates the integration with MaterialService.

**Independent Test**: Can be tested by selecting different materials for the same part and verifying prices vary according to material properties (density, cost per kg, process parameters).

**Acceptance Scenarios**:

1. **Given** a customer selects a material with higher density, **When** the price is calculated, **Then** the material cost component is proportionally higher
2. **Given** a customer selects a material with specific process parameters (flow rate, layer time, machinability), **When** the price is calculated, **Then** these parameters affect the machine time cost appropriately

---

### User Story 5 - Quote Details Are Auditable (Priority: P3)

As a business owner, I want all pricing calculations to be logged for audit purposes, so that I can review how each quote was determined and resolve customer disputes.

**Why this priority**: Important for business operations and dispute resolution, but secondary to the core pricing functionality.

**Independent Test**: Can be tested by generating quotes and verifying audit records are persisted with key details (technology, inputs, outputs).

**Acceptance Scenarios**:

1. **Given** a price is calculated, **When** the calculation completes, **Then** an audit record is persisted with the technology used, input parameters, and resulting price
2. **Given** an audit record exists, **When** a dispute arises, **Then** the business can review how the price was calculated

---

### Edge Cases

- What happens when a part has extremely low volume but high height (hollow basket)? The minimum layer time logic ensures reasonable print time estimates regardless of sparse geometry.
- What happens when CNC part volume equals or exceeds the material block? The removal volume calculation handles zero or negative removal gracefully (minimum floor still applies).
- What happens when material service is unavailable? The pricing request should fail gracefully with a clear error message indicating the dependency issue.
- What happens when a customer requests Scanning with Reverse Engineering tier? The price returns 4,500 THB minimum (vs 2,500 THB for raw scan).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST remove the existing ML-based pricing engine entirely from the codebase
- **FR-002**: System MUST remove ML.net package dependencies from the project
- **FR-003**: System MUST remove MLEnhanced and Hybrid options from the PricingStrategy enum, keeping only RuleBased and Manual
- **FR-004**: System MUST support five manufacturing technologies: FDM, SLA, CNC, Scanning, and Design
- **FR-005**: System MUST calculate FDM pricing based on: material volume, support structures (+20%), layer height, height, volumetric flow rate, minimum layer time, material density, and material cost
- **FR-006**: System MUST calculate SLA pricing based on: layer count, layer exposure time, lift time, material density, and material cost
- **FR-007**: System MUST calculate CNC pricing based on: bounding box dimensions, part volume, material removal volume, machinability rating, complexity factor, material block cost, and setup fees
- **FR-008**: System MUST return fixed minimum prices for Scanning (2,500 THB raw, 4,500 THB with reverse engineering) and Design (500 THB)
- **FR-009**: System MUST enforce minimum price floors: FDM ≥300 THB, SLA ≥500 THB, CNC ≥2,500 THB
- **FR-010**: System MUST include explanatory notes for Scanning and Design estimates indicating final prices are confirmed manually
- **FR-011**: System MUST apply the minimum layer time constraint for FDM when calculated layer time falls below the threshold
- **FR-012**: System MUST retrieve material properties (density, cost per kg, process parameters) from MaterialService
- **FR-013**: System MUST use configurable machine hourly rates and setup fees (not stored in database)
- **FR-014**: System MUST generate automatic FDM-based estimates when files are uploaded, using default parameters (0.2mm layer height, no supports)
- **FR-015**: System MUST persist audit records for each pricing calculation
- **FR-016**: System MUST rename all references from "PrintingTechnology" to "ManufacturingTechnology"
- **FR-017**: System MUST produce identical prices for identical inputs across all calculations (deterministic behavior)

### Key Entities

- **PricingRequest**: Represents a customer's quote request including material selection, manufacturing technology, geometry metrics (volume, surface area, bounding box), layer height, support settings, and part height
- **MaterialData**: Properties retrieved from MaterialService including density (g/cm³), cost per kg (THB), and process parameters specific to each manufacturing technology
- **MachineRates**: Configurable hourly rates and setup fees for each manufacturing technology, plus material removal rate for CNC
- **PricingResult**: The calculated price broken down by components (material cost, machine time, setup fee), plus notes for manual-confirmation scenarios
- **PricingAuditRecord**: Historical record of pricing calculations for dispute resolution and analysis

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Identical pricing requests submitted multiple times return prices that are exactly the same (zero variance)
- **SC-002**: Customers can complete a quote request in under 2 minutes from file upload to price display
- **SC-003**: All five manufacturing technologies (FDM, SLA, CNC, Scanning, Design) produce valid pricing results
- **SC-004**: Minimum price floors are enforced 100% of the time (no quote below floor for each technology)
- **SC-005**: Customer complaints about inconsistent pricing drop to zero
- **SC-006**: Audit records are persisted for 100% of pricing calculations
- **SC-007**: Individual pricing calculations complete in under 1 second

## Clarifications

### Session 2026-02-22

- Q: What is the maximum acceptable response time for a single pricing calculation? → A: Under 1 second (standard API response time)

## Assumptions

- The MaterialService already has Density, CostPerKg, and ProcessParameters fields available (dependency noted in requirements)
- Machine rates (hourly costs, setup fees) are known values that Natthapol will provide or adjust as needed
- Initial placeholder machine rates are acceptable for development and testing
- The existing geometry analysis (volume, surface area, bounding box) already provides the required metrics
- Scanning and Design services will always require manual confirmation after initial estimate
- The term "PrintingTechnology" may exist in the current codebase and needs renaming to "ManufacturingTechnology"
- Backwards compatibility for ManufacturingProcessId in PricingRequest should be maintained
