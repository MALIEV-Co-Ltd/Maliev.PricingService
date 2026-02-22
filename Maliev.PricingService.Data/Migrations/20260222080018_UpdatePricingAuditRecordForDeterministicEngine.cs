using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePricingAuditRecordForDeterministicEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pricing_audit_records_pricing_configurations_PricingConfigu~",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "ConfigMachineHourlyRate",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "ConfigMarginMultiplier",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "ConfigMaterialPricePerCm3",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "ConfigSupportPricePerCm3",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "MLModelVersion",
                table: "pricing_audit_records");

            migrationBuilder.AlterColumn<Guid>(
                name: "PricingConfigurationId",
                table: "pricing_audit_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "ManufacturingProcessName",
                table: "pricing_audit_records",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "ManufacturingProcessId",
                table: "pricing_audit_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Technology",
                table: "pricing_audit_records",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_audit_records_pricing_configurations_PricingConfigu~",
                table: "pricing_audit_records",
                column: "PricingConfigurationId",
                principalTable: "pricing_configurations",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pricing_audit_records_pricing_configurations_PricingConfigu~",
                table: "pricing_audit_records");

            migrationBuilder.DropColumn(
                name: "Technology",
                table: "pricing_audit_records");

            migrationBuilder.AlterColumn<Guid>(
                name: "PricingConfigurationId",
                table: "pricing_audit_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ManufacturingProcessName",
                table: "pricing_audit_records",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ManufacturingProcessId",
                table: "pricing_audit_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfigMachineHourlyRate",
                table: "pricing_audit_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfigMarginMultiplier",
                table: "pricing_audit_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfigMaterialPricePerCm3",
                table: "pricing_audit_records",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfigSupportPricePerCm3",
                table: "pricing_audit_records",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MLModelVersion",
                table: "pricing_audit_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_pricing_audit_records_pricing_configurations_PricingConfigu~",
                table: "pricing_audit_records",
                column: "PricingConfigurationId",
                principalTable: "pricing_configurations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
