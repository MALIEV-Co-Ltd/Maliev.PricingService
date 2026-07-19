namespace Maliev.PricingService.Application.Interfaces;

/// <summary>
/// Client for communicating with CurrencyService.
/// </summary>
public interface ICurrencyServiceClient
{
    /// <summary>
    /// Converts an amount from one currency to another.
    /// </summary>
    /// <param name="amount">The amount to convert.</param>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The converted amount.</returns>
    Task<decimal> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the exchange rate between two currencies.
    /// </summary>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange rate.</returns>
    Task<decimal> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);
}
