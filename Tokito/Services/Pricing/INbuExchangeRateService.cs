namespace Tokito.Services.Pricing
{
    public interface INbuExchangeRateService
    {
        Task<IReadOnlyDictionary<string, ExchangeRateQuote>> GetRatesToUahAsync(CancellationToken cancellationToken = default);
    }

    public sealed record ExchangeRateQuote(string CurrencyCode, decimal RateToUah, DateOnly ExchangeDate);
}
