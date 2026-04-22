using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.Games
{
    public interface IGameService
    {
        Task<GameViewDTO?> GetGameByIdAsync(int id, int? userId = null, string? countryCode = null);

        Task<List<GameViewDTO>> GetGamesByGenresAsync(string? genre, int? userId = null, string? countryCode = null);

        Task<List<GameViewDTO>> GetLibraryAsync(int userId, string? genre = null, string? countryCode = null);

        Task<CreatedGameDto> CreateGameAsync(int publisherId, CreateGameDto dto, CancellationToken cancellationToken = default);

        Task<CreatedGameDto> UpdateGameAsync(int gameId, int publisherId, UpdateGameDto dto, CancellationToken cancellationToken = default);

        Task<GameDashboardDto> GetDashboardAsync(
            int? requestingPublisherId,
            bool isUserAdmin,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            int? gameId,
            int? publisherId,
            CancellationToken cancellationToken = default);

        Task<ReviewResult> AddReviewAsync(int userId, int gameId, CreateReviewDto dto);

        Task<PurchaseGameResult> PurchaseGameAsync(int userId, int gameId);

        Task<DeleteGameResult> DeleteGameByIdAsync(int gameId, int userId, bool isUserAdmin);
    }
}
