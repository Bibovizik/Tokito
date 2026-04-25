namespace Tokito.DTOs.Genres
{
    public class GenreViewDto
    {
        public int GenreId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
