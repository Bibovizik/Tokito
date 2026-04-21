using System.ComponentModel.DataAnnotations;

namespace Tokito.Models;

public class Transaction
{
    [Key]
    public int TransactionId { get; set; }

    public int UserId { get; set; }
    public int GameId { get; set; }

    public DateTime PurchaseDate { get; set; }
    public decimal AmountPaid { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal BasePriceUahSnapshot { get; set; }
    public decimal ExchangeRateSnapshot { get; set; }
    public virtual User User { get; set; } = null!;
    public virtual Game Game { get; set; } = null!;
}