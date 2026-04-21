using System.ComponentModel.DataAnnotations;

namespace Tokito.DTOs.GameReviewDTOs
{
    public class CreateReviewDto
    {
        [Range(1, 10)]
        public int Score { get; set; }

        [MaxLength(1000)]
        public string? Review { get; set; }
    }
}
