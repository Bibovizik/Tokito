using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.WalletDTOs
{
    public class WalletTopUpDto
    {
        public decimal Amount { get; set; }

        public decimal? ExchangeRateToUahSnapshot { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }
    }
}
