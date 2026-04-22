using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.GameDTOs
{
    public class CreateGameMarketPriceOverrideDto
    {
        [Required]
        [StringLength(16, MinimumLength = 2)]
        public string MarketCode { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }
    }
}
