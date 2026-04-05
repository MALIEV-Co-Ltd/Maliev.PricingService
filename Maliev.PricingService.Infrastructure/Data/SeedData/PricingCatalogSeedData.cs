using System.Security.Cryptography;
using System.Text;
using Maliev.PricingService.Domain.Entities;

namespace Maliev.PricingService.Infrastructure.Data.SeedData;

/// <summary>
/// Provides static seed data for lead time options and volume discount tiers.
/// </summary>
public static class PricingCatalogSeedData
{
    private static Guid G(string code)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    public static IEnumerable<LeadTimeOption> GetLeadTimeOptions() =>
    [
        new() { Id = G("LT_ECONOMY"), Code = "ECONOMY", Name = "Economy", MinBusinessDays = 10, MaxBusinessDays = 14, PriceMultiplier = 0.85m, IsDefault = false, IsActive = true, SortOrder = 10 },
        new() { Id = G("LT_STANDARD"), Code = "STANDARD", Name = "Standard", MinBusinessDays = 5, MaxBusinessDays = 7, PriceMultiplier = 1.00m, IsDefault = true, IsActive = true, SortOrder = 20 },
        new() { Id = G("LT_EXPRESS"), Code = "EXPRESS", Name = "Express", MinBusinessDays = 2, MaxBusinessDays = 3, PriceMultiplier = 1.30m, IsDefault = false, IsActive = true, SortOrder = 30 },
    ];

    public static IEnumerable<VolumeDiscountTier> GetVolumeDiscountTiers() =>
    [
        new() { Id = G("VD_1_4"), MinQuantity = 1, MaxQuantity = 4, DiscountPercent = 0m, IsActive = true, SortOrder = 10 },
        new() { Id = G("VD_5_9"), MinQuantity = 5, MaxQuantity = 9, DiscountPercent = 5m, IsActive = true, SortOrder = 20 },
        new() { Id = G("VD_10_24"), MinQuantity = 10, MaxQuantity = 24, DiscountPercent = 10m, IsActive = true, SortOrder = 30 },
        new() { Id = G("VD_25_49"), MinQuantity = 25, MaxQuantity = 49, DiscountPercent = 18m, IsActive = true, SortOrder = 40 },
        new() { Id = G("VD_50_99"), MinQuantity = 50, MaxQuantity = 99, DiscountPercent = 22m, IsActive = true, SortOrder = 50 },
        new() { Id = G("VD_100_PLUS"), MinQuantity = 100, MaxQuantity = null, DiscountPercent = 28m, IsActive = true, SortOrder = 60 },
    ];

    public static IEnumerable<MachineCapacityConfig> GetMachineCapacityConfigs() =>
    [
        new() { Id = G("MCC_FDM"), ProcessType = "FDM", MachineCount = 3,
                AvgThroughputPartsPerDay = 5m, CurrentQueueDepth = 0,
                SetupTimeDays = 1, ShippingBufferDays = 2, IsActive = true },
        new() { Id = G("MCC_SLA"), ProcessType = "SLA", MachineCount = 2,
                AvgThroughputPartsPerDay = 3m, CurrentQueueDepth = 0,
                SetupTimeDays = 1, ShippingBufferDays = 2, IsActive = true },
        new() { Id = G("MCC_CNC"), ProcessType = "CNC", MachineCount = 1,
                AvgThroughputPartsPerDay = 1m, CurrentQueueDepth = 0,
                SetupTimeDays = 2, ShippingBufferDays = 3, IsActive = true },
        new() { Id = G("MCC_CNC_MILL"), ProcessType = "CNC_MILL", MachineCount = 2,
                AvgThroughputPartsPerDay = 2m, CurrentQueueDepth = 0,
                SetupTimeDays = 2, ShippingBufferDays = 3, IsActive = true },
        new() { Id = G("MCC_CNC_TURN"), ProcessType = "CNC_TURN", MachineCount = 1,
                AvgThroughputPartsPerDay = 4m, CurrentQueueDepth = 0,
                SetupTimeDays = 1, ShippingBufferDays = 3, IsActive = true },
        new() { Id = G("MCC_SLS"), ProcessType = "SLS", MachineCount = 1,
                AvgThroughputPartsPerDay = 8m, CurrentQueueDepth = 0,
                SetupTimeDays = 1, ShippingBufferDays = 2, IsActive = true },
        new() { Id = G("MCC_MJF"), ProcessType = "MJF", MachineCount = 1,
                AvgThroughputPartsPerDay = 10m, CurrentQueueDepth = 0,
                SetupTimeDays = 1, ShippingBufferDays = 2, IsActive = true },
        new() { Id = G("MCC_MJ"), ProcessType = "MJ", MachineCount = 1,
                AvgThroughputPartsPerDay = 4m, CurrentQueueDepth = 0,
                SetupTimeDays = 1, ShippingBufferDays = 2, IsActive = true },
        new() { Id = G("MCC_BJ"), ProcessType = "BJ", MachineCount = 1,
                AvgThroughputPartsPerDay = 6m, CurrentQueueDepth = 0,
                SetupTimeDays = 2, ShippingBufferDays = 3, IsActive = true },
        new() { Id = G("MCC_DMLS"), ProcessType = "DMLS", MachineCount = 1,
                AvgThroughputPartsPerDay = 2m, CurrentQueueDepth = 0,
                SetupTimeDays = 2, ShippingBufferDays = 3, IsActive = true },
    ];

    // ── Process IDs (must match MaterialService seed data) ─────────────────────
    private static readonly Guid CncId = G("PROC_CNC");
    private static readonly Guid CncMillId = G("PROC_CNC_MILL");
    private static readonly Guid CncTurnId = G("PROC_CNC_TURN");
    private static readonly Guid FdmId = G("PROC_FDM");
    private static readonly Guid SlaDlpId = G("PROC_SLA_DLP");
    private static readonly Guid SlsId = G("PROC_SLS");
    private static readonly Guid MjfId = G("PROC_MJF");
    private static readonly Guid MjId = G("PROC_MJ");
    private static readonly Guid BjId = G("PROC_BJ");
    private static readonly Guid DmlsId = G("PROC_DMLS");

    // ── Material IDs (must match MaterialService seed data) ───────────────────
    // CNC Metals
    private static readonly Guid Al6061Id = G("MAT_AL6061");
    private static readonly Guid Al7075Id = G("MAT_AL7075");
    private static readonly Guid Ss304Id = G("MAT_SS304");
    private static readonly Guid Ss316LId = G("MAT_SS316L");
    private static readonly Guid BrassC360Id = G("MAT_BRASS_C360");
    private static readonly Guid CopperC110Id = G("MAT_COPPER_C110");
    private static readonly Guid Ti6Al4VId = G("MAT_TI6AL4V");
    // CNC Polymers
    private static readonly Guid PeekId = G("MAT_PEEK");
    private static readonly Guid DelrinId = G("MAT_DELRIN");
    // FDM Materials
    private static readonly Guid PlaId = G("MAT_PLA");
    private static readonly Guid PetgId = G("MAT_PETG");
    private static readonly Guid AbsId = G("MAT_ABS");
    private static readonly Guid Pa12Id = G("MAT_PA12");
    private static readonly Guid Tpu95AId = G("MAT_TPU95A");
    private static readonly Guid AsaId = G("MAT_ASA");
    private static readonly Guid PcId = G("MAT_PC");
    private static readonly Guid CfPetgId = G("MAT_CF_PETG");
    // SLA/DLP Resins
    private static readonly Guid StdResinId = G("MAT_STD_RESIN");
    private static readonly Guid ToughResinId = G("MAT_TOUGH_RESIN");
    private static readonly Guid FlexResinId = G("MAT_FLEX_RESIN");
    private static readonly Guid CastResinId = G("MAT_CAST_RESIN");
    private static readonly Guid HtResinId = G("MAT_HT_RESIN");
    // SLS Materials
    private static readonly Guid SlsPa12Id = G("MAT_PA12_SLS");
    private static readonly Guid SlsPa11Id = G("MAT_PA11_SLS");
    private static readonly Guid SlsPa12GfId = G("MAT_PA12GF_SLS");
    // MJF Materials
    private static readonly Guid MjfPa12Id = G("MAT_PA12_MJF");
    private static readonly Guid MjfPa12GbId = G("MAT_PA12GB_MJF");
    // Material Jetting
    private static readonly Guid MjVeroWhiteId = G("MAT_VEROWHITE");
    private static readonly Guid MjVeroBlackId = G("MAT_VEROBLACK");
    private static readonly Guid MjTangoPlusId = G("MAT_TANGOPLUS");
    // Binder Jetting
    private static readonly Guid BjSs316LId = G("MAT_SS316L_BJ");
    private static readonly Guid BjBronzeId = G("MAT_BRONZE_BJ");
    private static readonly Guid BjSandId = G("MAT_SAND_BJ");
    // DMLS
    private static readonly Guid DmlsTi6Al4VId = G("MAT_TI6AL4V_DMLS");
    private static readonly Guid DmlsAlSi10MgId = G("MAT_ALSI10MG");
    private static readonly Guid DmlsIn718Id = G("MAT_IN718");
    private static readonly Guid Dmls174PhId = G("MAT_174PH");

    /// <summary>
    /// Returns pricing configurations for all material + process combinations.
    /// IDs must match MaterialService seed data for foreign key consistency.
    /// </summary>
    public static IEnumerable<PricingConfiguration> GetPricingConfigurations() =>
    [
        // ══ CNC Machining ══════════════════════════════════════════════════════
        // CNC + Metals (material cost is per gram, not cm3)
        new() { Id = G("PC_CNC_AL6061"), MaterialId = Al6061Id, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.18m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 45m, SetupCostFlat = 800m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_AL7075"), MaterialId = Al7075Id, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.32m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 40m, SetupCostFlat = 800m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_SS304"), MaterialId = Ss304Id, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.12m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 950m,
                PrintSpeedCm3PerHour = 25m, SetupCostFlat = 1000m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_SS316L"), MaterialId = Ss316LId, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.15m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 950m,
                PrintSpeedCm3PerHour = 22m, SetupCostFlat = 1000m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_BRASS"), MaterialId = BrassC360Id, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.45m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 55m, SetupCostFlat = 800m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_COPPER"), MaterialId = CopperC110Id, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.55m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 950m,
                PrintSpeedCm3PerHour = 20m, SetupCostFlat = 1200m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_TI"), MaterialId = Ti6Al4VId, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 1.20m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 1200m,
                PrintSpeedCm3PerHour = 15m, SetupCostFlat = 1500m, MinimumOrderPrice = 3000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        // CNC + Engineering Polymers
        new() { Id = G("PC_CNC_PEEK"), MaterialId = PeekId, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.85m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 35m, SetupCostFlat = 600m, MinimumOrderPrice = 2000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_CNC_DELRIN"), MaterialId = DelrinId, ManufacturingProcessId = CncId,
                MaterialPricePerCm3 = 0.35m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 50m, SetupCostFlat = 600m, MinimumOrderPrice = 2000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ FDM 3D Printing ══════════════════════════════════════════════════
        new() { Id = G("PC_FDM_PLA"), MaterialId = PlaId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.055m, SupportMaterialPricePerCm3 = 0.035m, MachineHourlyRate = 120m,
                PrintSpeedCm3PerHour = 20m, SetupCostFlat = 150m, MinimumOrderPrice = 300m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_PETG"), MaterialId = PetgId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.065m, SupportMaterialPricePerCm3 = 0.040m, MachineHourlyRate = 120m,
                PrintSpeedCm3PerHour = 18m, SetupCostFlat = 150m, MinimumOrderPrice = 300m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_ABS"), MaterialId = AbsId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.060m, SupportMaterialPricePerCm3 = 0.038m, MachineHourlyRate = 130m,
                PrintSpeedCm3PerHour = 22m, SetupCostFlat = 150m, MinimumOrderPrice = 300m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_PA12"), MaterialId = Pa12Id, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.120m, SupportMaterialPricePerCm3 = 0.080m, MachineHourlyRate = 140m,
                PrintSpeedCm3PerHour = 15m, SetupCostFlat = 180m, MinimumOrderPrice = 350m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_TPU"), MaterialId = Tpu95AId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.110m, SupportMaterialPricePerCm3 = 0.075m, MachineHourlyRate = 130m,
                PrintSpeedCm3PerHour = 12m, SetupCostFlat = 180m, MinimumOrderPrice = 350m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_ASA"), MaterialId = AsaId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.070m, SupportMaterialPricePerCm3 = 0.042m, MachineHourlyRate = 130m,
                PrintSpeedCm3PerHour = 20m, SetupCostFlat = 150m, MinimumOrderPrice = 300m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_PC"), MaterialId = PcId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.090m, SupportMaterialPricePerCm3 = 0.055m, MachineHourlyRate = 140m,
                PrintSpeedCm3PerHour = 16m, SetupCostFlat = 180m, MinimumOrderPrice = 350m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_FDM_CF_PETG"), MaterialId = CfPetgId, ManufacturingProcessId = FdmId,
                MaterialPricePerCm3 = 0.130m, SupportMaterialPricePerCm3 = 0.080m, MachineHourlyRate = 140m,
                PrintSpeedCm3PerHour = 14m, SetupCostFlat = 200m, MinimumOrderPrice = 400m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ SLA/DLP 3D Printing ════════════════════════════════════════════
        new() { Id = G("PC_SLA_STD"), MaterialId = StdResinId, ManufacturingProcessId = SlaDlpId,
                MaterialPricePerCm3 = 0.45m, SupportMaterialPricePerCm3 = 0.35m, MachineHourlyRate = 250m,
                PrintSpeedCm3PerHour = 10m, SetupCostFlat = 200m, MinimumOrderPrice = 500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_SLA_TOUGH"), MaterialId = ToughResinId, ManufacturingProcessId = SlaDlpId,
                MaterialPricePerCm3 = 0.55m, SupportMaterialPricePerCm3 = 0.40m, MachineHourlyRate = 250m,
                PrintSpeedCm3PerHour = 8m, SetupCostFlat = 200m, MinimumOrderPrice = 500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_SLA_FLEX"), MaterialId = FlexResinId, ManufacturingProcessId = SlaDlpId,
                MaterialPricePerCm3 = 0.65m, SupportMaterialPricePerCm3 = 0.50m, MachineHourlyRate = 250m,
                PrintSpeedCm3PerHour = 7m, SetupCostFlat = 200m, MinimumOrderPrice = 500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_SLA_CAST"), MaterialId = CastResinId, ManufacturingProcessId = SlaDlpId,
                MaterialPricePerCm3 = 0.80m, SupportMaterialPricePerCm3 = 0.60m, MachineHourlyRate = 280m,
                PrintSpeedCm3PerHour = 6m, SetupCostFlat = 250m, MinimumOrderPrice = 600m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_SLA_HT"), MaterialId = HtResinId, ManufacturingProcessId = SlaDlpId,
                MaterialPricePerCm3 = 0.90m, SupportMaterialPricePerCm3 = 0.70m, MachineHourlyRate = 280m,
                PrintSpeedCm3PerHour = 5m, SetupCostFlat = 250m, MinimumOrderPrice = 600m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ CNC Milling ════════════════════════════════════════════════════════
        new() { Id = G("PC_MILL_AL6061"), MaterialId = Al6061Id, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.18m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 45m, SetupCostFlat = 800m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_AL7075"), MaterialId = Al7075Id, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.32m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 40m, SetupCostFlat = 800m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_SS304"), MaterialId = Ss304Id, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.12m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 950m,
                PrintSpeedCm3PerHour = 25m, SetupCostFlat = 1000m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_SS316L"), MaterialId = Ss316LId, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.15m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 950m,
                PrintSpeedCm3PerHour = 22m, SetupCostFlat = 1000m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_BRASS"), MaterialId = BrassC360Id, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.45m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 55m, SetupCostFlat = 800m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_COPPER"), MaterialId = CopperC110Id, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.55m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 950m,
                PrintSpeedCm3PerHour = 20m, SetupCostFlat = 1200m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_TI"), MaterialId = Ti6Al4VId, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 1.20m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 1200m,
                PrintSpeedCm3PerHour = 15m, SetupCostFlat = 1500m, MinimumOrderPrice = 3000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_PEEK"), MaterialId = PeekId, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.85m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 35m, SetupCostFlat = 600m, MinimumOrderPrice = 2000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MILL_DELRIN"), MaterialId = DelrinId, ManufacturingProcessId = CncMillId,
                MaterialPricePerCm3 = 0.35m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 50m, SetupCostFlat = 600m, MinimumOrderPrice = 2000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ CNC Turning ════════════════════════════════════════════════════════
        new() { Id = G("PC_TURN_AL6061"), MaterialId = Al6061Id, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.18m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 80m, SetupCostFlat = 500m, MinimumOrderPrice = 1500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_AL7075"), MaterialId = Al7075Id, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.32m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 70m, SetupCostFlat = 500m, MinimumOrderPrice = 1500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_SS304"), MaterialId = Ss304Id, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.12m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 40m, SetupCostFlat = 600m, MinimumOrderPrice = 1500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_SS316L"), MaterialId = Ss316LId, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.15m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 35m, SetupCostFlat = 600m, MinimumOrderPrice = 1500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_BRASS"), MaterialId = BrassC360Id, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.45m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 750m,
                PrintSpeedCm3PerHour = 90m, SetupCostFlat = 500m, MinimumOrderPrice = 1500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_COPPER"), MaterialId = CopperC110Id, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.55m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 850m,
                PrintSpeedCm3PerHour = 30m, SetupCostFlat = 700m, MinimumOrderPrice = 1500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_TI"), MaterialId = Ti6Al4VId, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 1.20m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 1100m,
                PrintSpeedCm3PerHour = 20m, SetupCostFlat = 800m, MinimumOrderPrice = 2000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_TURN_DELRIN"), MaterialId = DelrinId, ManufacturingProcessId = CncTurnId,
                MaterialPricePerCm3 = 0.35m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 650m,
                PrintSpeedCm3PerHour = 80m, SetupCostFlat = 400m, MinimumOrderPrice = 1200m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ SLS 3D Printing ════════════════════════════════════════════════════
        new() { Id = G("PC_SLS_PA12"), MaterialId = SlsPa12Id, ManufacturingProcessId = SlsId,
                MaterialPricePerCm3 = 0.10m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 180m,
                PrintSpeedCm3PerHour = 15m, SetupCostFlat = 200m, MinimumOrderPrice = 400m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_SLS_PA11"), MaterialId = SlsPa11Id, ManufacturingProcessId = SlsId,
                MaterialPricePerCm3 = 0.12m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 180m,
                PrintSpeedCm3PerHour = 14m, SetupCostFlat = 200m, MinimumOrderPrice = 400m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_SLS_PA12GF"), MaterialId = SlsPa12GfId, ManufacturingProcessId = SlsId,
                MaterialPricePerCm3 = 0.15m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 180m,
                PrintSpeedCm3PerHour = 12m, SetupCostFlat = 220m, MinimumOrderPrice = 450m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ MJF 3D Printing ════════════════════════════════════════════════════
        new() { Id = G("PC_MJF_PA12"), MaterialId = MjfPa12Id, ManufacturingProcessId = MjfId,
                MaterialPricePerCm3 = 0.12m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 200m,
                PrintSpeedCm3PerHour = 18m, SetupCostFlat = 200m, MinimumOrderPrice = 400m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MJF_PA12GB"), MaterialId = MjfPa12GbId, ManufacturingProcessId = MjfId,
                MaterialPricePerCm3 = 0.15m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 200m,
                PrintSpeedCm3PerHour = 16m, SetupCostFlat = 220m, MinimumOrderPrice = 450m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ Material Jetting ═══════════════════════════════════════════════════
        new() { Id = G("PC_MJ_VEROWHITE"), MaterialId = MjVeroWhiteId, ManufacturingProcessId = MjId,
                MaterialPricePerCm3 = 0.55m, SupportMaterialPricePerCm3 = 0.40m, MachineHourlyRate = 300m,
                PrintSpeedCm3PerHour = 8m, SetupCostFlat = 250m, MinimumOrderPrice = 600m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MJ_VEROBLACK"), MaterialId = MjVeroBlackId, ManufacturingProcessId = MjId,
                MaterialPricePerCm3 = 0.55m, SupportMaterialPricePerCm3 = 0.40m, MachineHourlyRate = 300m,
                PrintSpeedCm3PerHour = 8m, SetupCostFlat = 250m, MinimumOrderPrice = 600m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_MJ_TANGOPLUS"), MaterialId = MjTangoPlusId, ManufacturingProcessId = MjId,
                MaterialPricePerCm3 = 0.75m, SupportMaterialPricePerCm3 = 0.55m, MachineHourlyRate = 300m,
                PrintSpeedCm3PerHour = 5m, SetupCostFlat = 300m, MinimumOrderPrice = 700m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ Binder Jetting ════════════════════════════════════════════════════
        new() { Id = G("PC_BJ_SS316L"), MaterialId = BjSs316LId, ManufacturingProcessId = BjId,
                MaterialPricePerCm3 = 0.80m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 200m,
                PrintSpeedCm3PerHour = 15m, SetupCostFlat = 500m, MinimumOrderPrice = 1200m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_BJ_BRONZE"), MaterialId = BjBronzeId, ManufacturingProcessId = BjId,
                MaterialPricePerCm3 = 0.60m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 200m,
                PrintSpeedCm3PerHour = 18m, SetupCostFlat = 500m, MinimumOrderPrice = 1200m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_BJ_SAND"), MaterialId = BjSandId, ManufacturingProcessId = BjId,
                MaterialPricePerCm3 = 0.30m, SupportMaterialPricePerCm3 = 0m,MachineHourlyRate = 150m,
                PrintSpeedCm3PerHour = 25m, SetupCostFlat = 400m, MinimumOrderPrice = 800m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },

        // ══ DMLS Metal Printing ════════════════════════════════════════════════
        new() { Id = G("PC_DMLS_TI6AL4V"), MaterialId = DmlsTi6Al4VId, ManufacturingProcessId = DmlsId,
                MaterialPricePerCm3 = 2.50m, SupportMaterialPricePerCm3 = 1.80m, MachineHourlyRate = 600m,
                PrintSpeedCm3PerHour = 3m, SetupCostFlat = 1500m, MinimumOrderPrice = 3000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_DMLS_ALSI10MG"), MaterialId = DmlsAlSi10MgId, ManufacturingProcessId = DmlsId,
                MaterialPricePerCm3 = 1.20m, SupportMaterialPricePerCm3 = 0.90m, MachineHourlyRate = 500m,
                PrintSpeedCm3PerHour = 5m, SetupCostFlat = 1200m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_DMLS_IN718"), MaterialId = DmlsIn718Id, ManufacturingProcessId = DmlsId,
                MaterialPricePerCm3 = 3.50m, SupportMaterialPricePerCm3 = 2.50m, MachineHourlyRate = 600m,
                PrintSpeedCm3PerHour = 3m, SetupCostFlat = 2000m, MinimumOrderPrice = 4000m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
        new() { Id = G("PC_DMLS_174PH"), MaterialId = Dmls174PhId, ManufacturingProcessId = DmlsId,
                MaterialPricePerCm3 = 0.90m, SupportMaterialPricePerCm3 = 0.65m, MachineHourlyRate = 500m,
                PrintSpeedCm3PerHour = 6m, SetupCostFlat = 1200m, MinimumOrderPrice = 2500m,
                MarginMultiplier = 2.0m, EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreatedBy = "system" },
    ];
}
