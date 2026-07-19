using System.Security.Cryptography;
using System.Text;
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

            migrationBuilder.Sql(BuildStableCodeBackfillSql(
                "MaterialId",
                "MaterialCode",
                new Dictionary<string, string>
                {
                    ["MAT_AL6061"] = "AL6061",
                    ["MAT_AL7075"] = "AL7075",
                    ["MAT_SS304"] = "SS304",
                    ["MAT_SS316L"] = "SS316L",
                    ["MAT_BRASS_C360"] = "BRASS_C360",
                    ["MAT_COPPER_C110"] = "COPPER_C110",
                    ["MAT_TI6AL4V"] = "TI6AL4V",
                    ["MAT_PEEK"] = "PEEK",
                    ["MAT_DELRIN"] = "DELRIN",
                    ["MAT_PLA"] = "PLA",
                    ["MAT_PETG"] = "PETG",
                    ["MAT_ABS"] = "ABS",
                    ["MAT_PA12"] = "PA12",
                    ["MAT_TPU95A"] = "TPU95A",
                    ["MAT_ASA"] = "ASA",
                    ["MAT_PC"] = "PC",
                    ["MAT_CF_PETG"] = "CF_PETG",
                    ["MAT_STD_RESIN"] = "STD_RESIN",
                    ["MAT_TOUGH_RESIN"] = "TOUGH_RESIN",
                    ["MAT_FLEX_RESIN"] = "FLEX_RESIN",
                    ["MAT_CAST_RESIN"] = "CAST_RESIN",
                    ["MAT_HT_RESIN"] = "HT_RESIN",
                    ["MAT_PA12_SLS"] = "PA12_SLS",
                    ["MAT_PA11_SLS"] = "PA11_SLS",
                    ["MAT_PA12GF_SLS"] = "PA12GF_SLS",
                    ["MAT_PA12_MJF"] = "PA12_MJF",
                    ["MAT_PA12GB_MJF"] = "PA12GB_MJF",
                    ["MAT_VEROWHITE"] = "VEROWHITE",
                    ["MAT_VEROBLACK"] = "VEROBLACK",
                    ["MAT_TANGOPLUS"] = "TANGOPLUS",
                    ["MAT_SS316L_BJ"] = "SS316L_BJ",
                    ["MAT_BRONZE_BJ"] = "BRONZE_BJ",
                    ["MAT_SAND_BJ"] = "SAND_BJ",
                    ["MAT_TI6AL4V_DMLS"] = "TI6AL4V_DMLS",
                    ["MAT_ALSI10MG"] = "ALSI10MG",
                    ["MAT_IN718"] = "IN718",
                    ["MAT_174PH"] = "174PH",
                }));

            migrationBuilder.Sql(BuildStableCodeBackfillSql(
                "ManufacturingProcessId",
                "ManufacturingProcessCode",
                new Dictionary<string, string>
                {
                    ["PROC_CNC"] = "CNC",
                    ["PROC_CNC_MILL"] = "CNC_MILL",
                    ["PROC_CNC_TURN"] = "CNC_TURN",
                    ["PROC_FDM"] = "FDM",
                    ["PROC_SLA_DLP"] = "SLA_DLP",
                    ["PROC_SLS"] = "SLS",
                    ["PROC_MJF"] = "MJF",
                    ["PROC_MJ"] = "MJ",
                    ["PROC_BJ"] = "BJ",
                    ["PROC_DMLS"] = "DMLS",
                }));

            migrationBuilder.Sql("""
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM pricing_configurations
                        WHERE "MaterialCode" = ''
                           OR "ManufacturingProcessCode" = '')
                    THEN
                        RAISE EXCEPTION
                            'Pricing stable code backfill failed: unknown material or manufacturing process identifiers remain.';
                    END IF;
                END
                $migration$;
                """);

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

        private static string BuildStableCodeBackfillSql(
            string idColumn,
            string codeColumn,
            IReadOnlyDictionary<string, string> stableCodes)
        {
            var values = string.Join(
                ",\n",
                stableCodes.Select(pair => $"('{CreateStableId(pair.Key)}'::uuid, '{pair.Value}')"));

            return $"""
                UPDATE pricing_configurations AS configuration
                SET "{codeColumn}" = stable_codes.code
                FROM (VALUES
                    {values}
                ) AS stable_codes(id, code)
                WHERE configuration."{idColumn}" = stable_codes.id
                  AND configuration."{codeColumn}" = '';
                """;
        }

        private static Guid CreateStableId(string code)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            var bytes = new byte[16];
            Array.Copy(hash, bytes, bytes.Length);
            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return new Guid(bytes);
        }
    }
}
