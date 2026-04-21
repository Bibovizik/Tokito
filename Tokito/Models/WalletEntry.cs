using System.ComponentModel.DataAnnotations;

namespace Tokito.Models;

public class WalletEntry
{
    public int WalletEntryId { get; set; }

    public int UserId { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public WalletEntryType EntryType { get; set; }

    public DateTime CreatedAt { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public int? TransactionId { get; set; }

    public decimal? ExchangeRateToUahSnapshot { get; set; }

    public decimal? AmountUahSnapshot { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Transaction? Transaction { get; set; }
}
