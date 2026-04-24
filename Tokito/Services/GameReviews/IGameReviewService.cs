using Tokito.DTOs.Common;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.GameReviews
{
    public interface IGameReviewService
    {
        Task<List<GameReviewViewDTO>> GetGameReviewsById(int id);

        Task<PagedResultDto<GameReviewViewDTO>> GetGameReviewsByIdPagedAsync(int id, int page, int pageSize);

        Task<ReviewResult> PostGameReviewById(int userId, int gameId, CreateReviewDto dto);
    }
}
