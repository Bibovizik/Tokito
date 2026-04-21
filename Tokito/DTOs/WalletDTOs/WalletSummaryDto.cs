namespace Tokito.DTOs.WalletDTOs
{
    public class WalletSummaryDto
    {
        public IReadOnlyCollection<WalletBalanceDto> Balances { get; set; } = Array.Empty<WalletBalanceDto>();
        public IReadOnlyCollection<WalletEntryDto> Entries { get; set; } = Array.Empty<WalletEntryDto>();
    }
}
