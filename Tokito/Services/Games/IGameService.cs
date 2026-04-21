using Microsoft.AspNetCore.Mvc;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Models;

namespace Tokito.Services.Games
{
    public interface IGameService
    {
        public Task<IReadOnlyCollection<Game>> GetGamesAsync();
        public Task<GameViewDTO?> GetGameByIdAsync(int id, int? userId = null, string? countryCode = null);
        public Task<int> AddReviewAsync(int userId, int gameId, CreateReviewDto dto);
        public Task<List<GameViewDTO>> GetGamesByGenresAsync(string genre);
        public Task<PurchaseGameResult> PurchaseGameAsync(int userId, int gameId);
    }
}
