using System.ComponentModel.DataAnnotations;

namespace Tokito.Models;

public class WalletBalance
{
    public int WalletBalanceId { get; set; }

    public int UserId { get; set; }

    public decimal AvailableAmount { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual User User { get; set; } = null!;
}
