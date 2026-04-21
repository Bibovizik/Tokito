namespace Tokito.DTOs.WalletDTOs
{
    public class WalletEntryDto
    {
        public int WalletEntryId { get; set; }
        public string CurrencyCode { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string EntryType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }
        public int? TransactionId { get; set; }
    }
}
