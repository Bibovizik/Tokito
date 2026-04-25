using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.Genres
{
    public class GenreUpsertDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
