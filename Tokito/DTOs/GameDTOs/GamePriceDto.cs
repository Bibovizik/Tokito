namespace Tokito.DTOs.GameDTOs
{
    public class GamePriceDto
    {
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = null!;
        public string CurrencySymbol { get; set; } = null!;
        public string Source { get; set; } = null!;
    }
}
