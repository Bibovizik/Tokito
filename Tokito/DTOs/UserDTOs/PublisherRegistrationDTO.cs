using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.UserDTOs
{
    public class PublisherRegistrationDTO
    {
        [Required]
        [StringLength(64)]
        public string UserNickname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(3, MinimumLength = 2)]
        public string CountryCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string PublisherName { get; set; } = string.Empty;

        public DateOnly? FoundationDate { get; set; }

        [Url]
        [StringLength(256)]
        public string? Website { get; set; }
    }
}
