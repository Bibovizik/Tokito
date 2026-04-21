namespace Tokito.DTOs.GameDTOs
{
    public class PurchaseReceiptDto
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = null!;
        public DateTime PurchasedAt { get; set; }
        public GamePriceDto ChargedPrice { get; set; } = null!;
        public decimal RemainingBalance { get; set; }
    }
}
