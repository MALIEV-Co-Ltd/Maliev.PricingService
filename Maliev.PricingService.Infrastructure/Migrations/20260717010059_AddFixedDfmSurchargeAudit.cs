using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedDfmSurchargeAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FixedDfmSurcharge",
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
                name: "FixedDfmSurcharge",
                table: "pricing_audit_records");
        }
    }
}
