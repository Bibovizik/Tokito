namespace Tokito.DTOs.WalletDTOs
{
    public class WalletBalanceDto
    {
        public string CurrencyCode { get; set; } = null!;
        public decimal AvailableAmount { get; set; }
    }
}
