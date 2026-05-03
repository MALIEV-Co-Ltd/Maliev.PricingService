using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lead_time_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinBusinessDays = table.Column<int>(type: "integer", nullable: false),
                    MaxBusinessDays = table.Column<int>(type: "integer", nullable: false),
                    PriceMultiplier = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_time_options", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "machine_capacity_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MachineCount = table.Column<int>(type: "integer", nullable: false),
                    AvgThroughputPartsPerDay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentQueueDepth = table.Column<int>(type: "integer", nullable: false),
                    SetupTimeDays = table.Column<int>(type: "integer", nullable: false),
                    ShippingBufferDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_capacity_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturingProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialPricePerCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SupportMaterialPricePerCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MachineHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrintSpeedCm3PerHour = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DensityGramPerCm3 = table.Column<decimal>(type: "numeric", nullable: true),
                    SetupCostFlat = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumOrderPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    MarginMultiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    ComplexityThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    ComplexitySurchargePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volume_discount_tiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volume_discount_tiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_audit_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputVolumeCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InputSupportVolumeCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InputSurfaceAreaCm2 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InputBoundingBoxX = table.Column<decimal>(type: "numeric", nullable: false),
                    InputBoundingBoxY = table.Column<decimal>(type: "numeric", nullable: false),
                    InputBoundingBoxZ = table.Column<decimal>(type: "numeric", nullable: false),
                    InputIsManifold = table.Column<bool>(type: "boolean", nullable: false),
                    InputTriangleCount = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufacturingProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturingProcessName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PricingConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigMaterialPricePerCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ConfigSupportPricePerCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ConfigMachineHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConfigMarginMultiplier = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Strategy = table.Column<int>(type: "integer", nullable: false),
                    MLModelVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaterialCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SupportMaterialCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MachineTimeCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SetupCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComplexitySurcharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SubtotalBeforeMargin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MarginAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VolumeDiscountTierId = table.Column<Guid>(type: "uuid", nullable: true),
                    VolumeDiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    VolumeDiscountAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConfidenceLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalculatedBySystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CalculationDuration = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_audit_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pricing_audit_records_pricing_configurations_PricingConfigu~",
                        column: x => x.PricingConfigurationId,
                        principalTable: "pricing_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    Technology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaterialBrand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LayerHeight = table.Column<decimal>(type: "numeric", nullable: true),
                    InfillPercentage = table.Column<decimal>(type: "numeric", nullable: true),
                    SupportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PrintOrientation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CalculatedPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ManualOverridePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    PricingAuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pricing_snapshots_pricing_audit_records_PricingAuditRecordId",
                        column: x => x.PricingAuditRecordId,
                        principalTable: "pricing_audit_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pricing_snapshots_pricing_snapshots_SupersededById",
                        column: x => x.SupersededById,
                        principalTable: "pricing_snapshots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_lead_time_options_Code",
                table: "lead_time_options",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_time_options_IsActive",
                table: "lead_time_options",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_machine_capacity_configs_IsActive",
                table: "machine_capacity_configs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_machine_capacity_configs_ProcessType",
                table: "machine_capacity_configs",
                column: "ProcessType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_audit_records_PricingConfigurationId",
                table: "pricing_audit_records",
                column: "PricingConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_configurations_MaterialId_ManufacturingProcessId_Ef~",
                table: "pricing_configurations",
                columns: new[] { "MaterialId", "ManufacturingProcessId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_snapshots_PricingAuditRecordId",
                table: "pricing_snapshots",
                column: "PricingAuditRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_snapshots_SupersededById",
                table: "pricing_snapshots",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_volume_discount_tiers_IsActive",
                table: "volume_discount_tiers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_volume_discount_tiers_MinQuantity",
                table: "volume_discount_tiers",
                column: "MinQuantity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_time_options");

            migrationBuilder.DropTable(
                name: "machine_capacity_configs");

            migrationBuilder.DropTable(
                name: "pricing_snapshots");

            migrationBuilder.DropTable(
                name: "volume_discount_tiers");

            migrationBuilder.DropTable(
                name: "pricing_audit_records");

            migrationBuilder.DropTable(
                name: "pricing_configurations");
        }
    }
}
