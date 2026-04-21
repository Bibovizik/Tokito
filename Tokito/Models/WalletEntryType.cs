namespace Tokito.Models;

public enum WalletEntryType : byte
{
    TopUp = 1,
    Purchase = 2,
    Refund = 3,
    Adjustment = 4,
    CurrencyConversion = 5
}
