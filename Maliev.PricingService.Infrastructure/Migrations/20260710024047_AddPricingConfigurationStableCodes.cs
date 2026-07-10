using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingConfigurationStableCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManufacturingProcessCode",
                table: "pricing_configurations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaterialCode",
                table: "pricing_configurations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_configurations_MaterialCode_ManufacturingProcessCod~",
                table: "pricing_configurations",
                columns: new[] { "MaterialCode", "ManufacturingProcessCode", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_pricing_configurations_MaterialId_ManufacturingProcessId_Is~",
                table: "pricing_configurations",
                columns: new[] { "MaterialId", "ManufacturingProcessId", "IsActive", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pricing_configurations_MaterialCode_ManufacturingProcessCod~",
                table: "pricing_configurations");

            migrationBuilder.DropIndex(
                name: "IX_pricing_configurations_MaterialId_ManufacturingProcessId_Is~",
                table: "pricing_configurations");

            migrationBuilder.DropColumn(
                name: "ManufacturingProcessCode",
                table: "pricing_configurations");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                table: "pricing_configurations");
        }
    }
}
