using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.DTOs.Genres;

namespace Tokito.DTOs.GameDTOs
{
    public class GameViewDTO
    {
        public int gameId { get; set; }

        public string Name { get; set; } = null!;

        public DateOnly? ReleaseDate { get; set; }

        public int? Rating { get; set; }

        public int PublisherId { get; set; }

        public JsonElement? SystemRequirements { get; set; }

        public int MostOneTimePlayers { get; set; }

        public string? PublisherName { get; set; }

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public virtual ICollection<GenreDTO> Genres { get; set; } = new List<GenreDTO>();

        public string? ImageUrl { get; set; }

        public List<GameReviewViewDTO> GameReviews { get; set; } = new();

        public decimal BasePriceUah { get; set; }

        public GamePriceDto? CurrentPrice { get; set; }

        public GamePriceDto? WalletPrice { get; set; }

        public bool IsOwnedByCurrentUser { get; set; }
    }
}
