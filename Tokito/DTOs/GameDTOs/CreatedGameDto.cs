using System.Text.Json;

namespace Tokito.DTOs.GameDTOs
{
    public class CreatedGameDto
    {
        public int GameId { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal BasePriceUah { get; set; }

        public int PublisherId { get; set; }

        public string PublisherName { get; set; } = string.Empty;

        public DateOnly? ReleaseDate { get; set; }

        public JsonElement? SystemRequirements { get; set; }

        public int MostOneTimePlayers { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public ICollection<string> Genres { get; set; } = [];

        public ICollection<CreatedGameMarketPriceDto> MarketPrices { get; set; } = [];
    }
}
