using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.GameDTOs
{
    public class GameUpsertFormDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateOnly? ReleaseDate { get; set; }

        public string? SystemRequirementsJson { get; set; }

        [Range(1, int.MaxValue)]
        public int MostOneTimePlayers { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal BasePriceUah { get; set; }

        [StringLength(2048)]
        public string? ImageUrl { get; set; }

        public IFormFile? Image { get; set; }

        public string? GenreIdsJson { get; set; }

        public string? MarketPriceOverridesJson { get; set; }

        public bool PreserveExistingImage { get; set; } = true;
    }
}
