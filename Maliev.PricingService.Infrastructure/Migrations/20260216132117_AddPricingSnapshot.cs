using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PricingService.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingSnapshots",
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
                    CalculatedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ManualOverridePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PricingAuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingSnapshots_PricingSnapshots_SupersededById",
                        column: x => x.SupersededById,
                        principalTable: "PricingSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingSnapshots_pricing_audit_records_PricingAuditRecordId",
                        column: x => x.PricingAuditRecordId,
                        principalTable: "pricing_audit_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingSnapshots_EmployeeId",
                table: "PricingSnapshots",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingSnapshots_OrderId",
                table: "PricingSnapshots",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingSnapshots_PricingAuditRecordId",
                table: "PricingSnapshots",
                column: "PricingAuditRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingSnapshots_SupersededById",
                table: "PricingSnapshots",
                column: "SupersededById",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingSnapshots");
        }
    }
}
