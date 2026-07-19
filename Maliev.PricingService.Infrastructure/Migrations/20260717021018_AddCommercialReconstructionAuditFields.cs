using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialReconstructionAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LeadTimeMultiplier",
                table: "pricing_audit_records",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineMarginAmountThb",
                table: "pricing_audit_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineSubtotalBeforeMarginThb",
                table: "pricing_audit_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderPriceFloorThb",
                table: "pricing_audit_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ToleranceMultiplier",
                table: "pricing_audit_records",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "VariableDfmSurcharge",
                table: "pricing_audit_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadTimeMultiplier",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "LineMarginAmountThb",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "LineSubtotalBeforeMarginThb",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "MinimumOrderPriceFloorThb",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "ToleranceMultiplier",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "VariableDfmSurcharge",
                table: "pricing_audit_records");
        }
    }
}
