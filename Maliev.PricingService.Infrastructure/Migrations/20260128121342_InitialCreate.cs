using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Application.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pricing_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturingProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialPricePerCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SupportMaterialPricePerCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MachineHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrintSpeedCm3PerHour = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "decode('0000000000000000', 'hex')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelType = table.Column<int>(type: "integer", nullable: false),
                    TrainingDataCount = table.Column<int>(type: "integer", nullable: false),
                    TrainingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrainingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrainingDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    MeanAbsoluteError = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MeanAbsolutePercentageError = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RSquared = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModelFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "decode('0000000000000000', 'hex')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
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
                    table.PrimaryKey("PK_pricing_audit_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_audit_records_pricing_configurations_PricingConfigu~",
                        column: x => x.PricingConfigurationId,
                        principalTable: "pricing_configurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_training_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PricingAuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JobCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JobSucceeded = table.Column<bool>(type: "boolean", nullable: false),
                    ActualMaterialUsedCm3 = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ActualPrintTimeHours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ActualLaborHours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ActualTotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualProfitMargin = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    UsedForTraining = table.Column<bool>(type: "boolean", nullable: false),
                    TrainedModelId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_training_data", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_training_data_pricing_audit_records_PricingAuditRec~",
                        column: x => x.PricingAuditRecordId,
                        principalTable: "pricing_audit_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pricing_training_data_pricing_models_TrainedModelId",
                        column: x => x.TrainedModelId,
                        principalTable: "pricing_models",
                        principalColumn: "id");
                });

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
                name: "IX_pricing_training_data_PricingAuditRecordId",
                table: "pricing_training_data",
                column: "PricingAuditRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_training_data_TrainedModelId",
                table: "pricing_training_data",
                column: "TrainedModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_training_data");

            migrationBuilder.DropTable(
                name: "pricing_audit_records");

            migrationBuilder.DropTable(
                name: "pricing_models");

            migrationBuilder.DropTable(
                name: "pricing_configurations");
        }
    }
}
