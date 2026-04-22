using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Tokito.DTOs.GameDTOs
{
    public class CreateGameDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateOnly? ReleaseDate { get; set; }

        public JsonElement? SystemRequirements { get; set; }

        [Range(1, int.MaxValue)]
        public int MostOneTimePlayers { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal BasePriceUah { get; set; }

        public string? ImageUrl { get; set; }

        public ICollection<int> GenreIds { get; set; } = [];

        public ICollection<CreateGameMarketPriceOverrideDto> MarketPriceOverrides { get; set; } = [];
    }
}
