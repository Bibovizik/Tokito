using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Tokito.Services.Pricing
{
    public class NbuExchangeRateService : INbuExchangeRateService
    {
        private static readonly CultureInfo ExchangeDateCulture = CultureInfo.InvariantCulture;
        private readonly HttpClient _httpClient;

        public NbuExchangeRateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyDictionary<string, ExchangeRateQuote>> GetRatesToUahAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetFromJsonAsync<List<NbuExchangeRateItem>>(
                "NBUStatService/v1/statdirectory/exchange?json",
                cancellationToken);

            if (response == null || response.Count == 0)
            {
                throw new InvalidOperationException("NBU exchange rates are unavailable.");
            }

            var quotes = new Dictionary<string, ExchangeRateQuote>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in response)
            {
                if (string.IsNullOrWhiteSpace(item.CurrencyCode) ||
                    string.IsNullOrWhiteSpace(item.ExchangeDate))
                {
                    continue;
                }

                if (!DateOnly.TryParseExact(item.ExchangeDate, "dd.MM.yyyy", ExchangeDateCulture, DateTimeStyles.None, out var exchangeDate))
                {
                    continue;
                }

                var currencyCode = item.CurrencyCode.Trim().ToUpperInvariant();
                quotes[currencyCode] = new ExchangeRateQuote(currencyCode, item.RateToUah, exchangeDate);
            }

            return quotes;
        }

        private sealed class NbuExchangeRateItem
        {
            [JsonPropertyName("cc")]
            public string CurrencyCode { get; set; } = string.Empty;

            [JsonPropertyName("rate")]
            public decimal RateToUah { get; set; }

            [JsonPropertyName("exchangedate")]
            public string ExchangeDate { get; set; } = string.Empty;
        }
    }
}
