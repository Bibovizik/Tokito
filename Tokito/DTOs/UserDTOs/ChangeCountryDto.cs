using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.UserDTOs
{
    public class ChangeCountryDto
    {
        [Required]
        [StringLength(3, MinimumLength = 2)]
        public string CountryCode { get; set; } = null!;

        [StringLength(200)]
        public string? Description { get; set; }
    }
}
