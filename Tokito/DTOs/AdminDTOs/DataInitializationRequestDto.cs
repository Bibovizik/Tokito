using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.AdminDTOs
{
    public class DataInitializationRequestDto
    {
        [Range(1, 200)]
        public int TestUserCount { get; set; } = 60;

        [Range(0, 15)]
        public int MaxPurchasesPerUser { get; set; } = 5;

        [Range(0, 10)]
        public int MaxReviewsPerUser { get; set; } = 3;

        [StringLength(260)]
        public string? SeedFilePath { get; set; }
    }
}
