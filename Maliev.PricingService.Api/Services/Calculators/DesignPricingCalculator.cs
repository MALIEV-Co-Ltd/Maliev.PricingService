namespace Maliev.PricingService.Api.Services.Calculators;

using System;
using System.Threading;
using System.Threading.Tasks;
using Maliev.PricingService.Api.Clients;

/// <summary>
/// Calculator for 3D Design technology.
/// </summary>
public class DesignPricingCalculator : IPricingCalculator
{
    private readonly IChatbotServiceClient _chatbotClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignPricingCalculator"/> class.
    /// </summary>
    /// <param name="chatbotClient">The chatbot service client.</param>
    public DesignPricingCalculator(IChatbotServiceClient chatbotClient)
    {
        _chatbotClient = chatbotClient;
    }

    /// <inheritdoc/>
    public ManufacturingTechnology Technology => ManufacturingTechnology.Design;

    /// <inheritdoc/>
    public async Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        // Call ChatbotService for design analysis if images are provided
        var analysis = (request.ReferenceImageUrls != null && request.ReferenceImageUrls.Count > 0)
            ? await _chatbotClient.AnalyzeDesignAsync(
                request.ReferenceImageUrls,
                request.DesignDescription,
                request.RequiresDrawings,
                cancellationToken)
            : null;

        // If ChatbotService unavailable or no images provided, use conservative defaults
        if (analysis == null)
        {
            decimal minPrice = 500m;
            return new PricingResult
            {
                Strategy = PricingStrategy.RuleBased,
                MaterialCost = 0,
                SupportMaterialCost = 0,
                MachineTimeCost = 0,
                SetupCost = minPrice,
                ComplexitySurcharge = 0,
                SubtotalBeforeMargin = minPrice,
                MarginAmount = 0,
                TotalUnitPrice = minPrice,
                TotalPrice = minPrice * request.Quantity,
                ConfidenceLevel = 1.0m,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                CalculationDuration = TimeSpan.Zero,
                Notes = "Design price is a minimum estimate. Final price confirmed after scope discussion."
            };
        }

        // Calculate from structured response
        decimal baseDesignHours = analysis.EstimatedDesignHours
                        + analysis.AdditionalCommunicationHours
                        + analysis.EstimatedDrawingHours;

        decimal techniqueMultiplier = analysis.ModelingTechnique switch
        {
            "StandardParametric" => 1.0m,
            "NonStandardPlane" => 1.2m,
            "ComplexSurface" => 1.5m,
            "MeshSculpting" => 1.8m,
            "SheetMetal" => 1.3m,
            _ => 1.0m
        };

        decimal designCost = baseDesignHours * rates.DesignHourlyRate * techniqueMultiplier;
        decimal scanningCost = analysis.RequiresPreScan ? rates.ScanningBasePrice : 0;
        decimal total = designCost + scanningCost;
        total = Math.Max(total, 500m); // minimum 500 THB

        return new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = 0,
            SupportMaterialCost = 0,
            MachineTimeCost = designCost, // repurpose as labor cost
            SetupCost = scanningCost,     // repurpose as pre-scan cost
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = total,
            MarginAmount = 0,
            TotalUnitPrice = total,
            TotalPrice = total * request.Quantity,
            ConfidenceLevel = analysis.ConfidenceLevel,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CalculationDuration = TimeSpan.Zero,
            Notes = $"Analysis: {analysis.ModelingTechnique}, {baseDesignHours:F1}h estimated. {analysis.Notes}"
        };
    }
}
