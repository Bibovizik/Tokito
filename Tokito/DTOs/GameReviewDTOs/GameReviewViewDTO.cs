namespace Tokito.DTOs.GameReviewDTOs
{
    public class GameReviewViewDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int Score { get; set; }
        public string? Review { get; set; }
        public DateTime RatedAt { get; set; }

    }
}
