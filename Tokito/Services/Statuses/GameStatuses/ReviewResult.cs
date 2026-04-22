using Tokito.DTOs.GameDTOs;

namespace Tokito.Services.Statuses.GameStatuses
{
    public class ReviewResult
    {
        public ReviewStatus Status { get; init; }
        public string? Message { get; init; }
        public static ReviewResult Success() =>
            new()
            {
                Status = ReviewStatus.Success,
            };

        public static ReviewResult Failure(ReviewStatus status, string message) =>
            new()
            {
                Status = status,
                Message = message
            };
    }
}
