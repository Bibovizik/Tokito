namespace Tokito.DTOs.GameDTOs
{
    public class CreatedGameMarketPriceDto
    {
        public string MarketCode { get; set; } = string.Empty;

        public string MarketName { get; set; } = string.Empty;

        public string CurrencyCode { get; set; } = string.Empty;

        public string CurrencySymbol { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Source { get; set; } = string.Empty;

        public decimal? ExchangeRateToUahSnapshot { get; set; }

        public DateOnly? ExchangeDate { get; set; }
    }
}
