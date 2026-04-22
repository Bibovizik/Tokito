using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.GameReviews
{
    public interface IGameReviewService
    {
        public Task<List<GameReviewViewDTO>> GetGameReviewsById(int id);
        public Task<ReviewResult> PostGameReviewById(int userId, int gameId, CreateReviewDto dto);
    }
}
