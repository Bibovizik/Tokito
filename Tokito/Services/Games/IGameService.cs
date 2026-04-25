using Microsoft.AspNetCore.Http;
using Tokito.DTOs.Common;
using Tokito.DTOs.GameDTOs;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.Games
{
    public interface IGameService
    {
        Task<GameViewDTO?> GetGameByIdAsync(int id, int? userId = null, string? countryCode = null);

        Task<List<GameViewDTO>> GetGamesAsync(
            string? name = null,
            IEnumerable<string>? genres = null,
            int? userId = null,
            string? countryCode = null);

        Task<PagedResultDto<GameViewDTO>> GetGamesPagedAsync(
            string? name,
            IEnumerable<string>? genres,
            int page,
            int pageSize,
            int? userId = null,
            string? countryCode = null);

        Task<List<GameViewDTO>> GetLibraryAsync(int userId, string? genre = null, string? countryCode = null);

        Task<PagedResultDto<GameViewDTO>> GetLibraryPagedAsync(
            int userId,
            int page,
            int pageSize,
            string? genre = null,
            string? countryCode = null);

        Task<CreatedGameDto> CreateGameAsync(
            int publisherId,
            CreateGameDto dto,
            IFormFile? imageFile = null,
            CancellationToken cancellationToken = default);

        Task<CreatedGameDto> UpdateGameAsync(
            int gameId,
            int publisherId,
            UpdateGameDto dto,
            IFormFile? imageFile = null,
            bool preserveExistingImage = true,
            CancellationToken cancellationToken = default);

        Task<GameDashboardDto> GetDashboardAsync(
            int? requestingPublisherId,
            bool isUserAdmin,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            int? gameId,
            int? publisherId,
            CancellationToken cancellationToken = default);

        Task<PurchaseGameResult> PurchaseGameAsync(int userId, int gameId);

        Task<DeleteGameResult> DeleteGameByIdAsync(int gameId, int userId, bool isUserAdmin);
    }
}
