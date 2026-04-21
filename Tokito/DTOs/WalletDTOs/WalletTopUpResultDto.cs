namespace Tokito.DTOs.WalletDTOs
{
    public class WalletTopUpResultDto
    {
        public string CurrencyCode { get; set; } = null!;
        public decimal CreditedAmount { get; set; }
        public decimal AvailableAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
