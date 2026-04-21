using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Tokito.Data;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.DTOs.Genres;
using Tokito.Models;

namespace Tokito.Services.Games
{
    public class GameService : IGameService
    {
        private IMapper _mapper;
        private GameStore _gameStore;
        public GameService(GameStore gameStore, IMapper mapper)
        {
            _mapper = mapper;
            _gameStore = gameStore;
        }

        public async Task<int> AddReviewAsync(int userId, int gameId, CreateReviewDto dto)
        {
            var newReview = new GameReview
            {
                UserId = userId,
                GameId = gameId,
                Score = dto.Score,
                Review = dto.Review,
                RatedAt = DateTime.UtcNow
            };

            _gameStore.GameReviews.Add(newReview);
            var result = await _gameStore.SaveChangesAsync();
            return result;
        }

        public async Task<GameViewDTO?> GetGameByIdAsync(int id)
        {
            var game = await _gameStore.Games.AsNoTracking().Include(g => g.Genres).Include(g => g.GameReviews).FirstOrDefaultAsync(g => g.GameId == id);
            if (game == null)
            {
                return null;
            }

            return new GameViewDTO
            {
                gameId = game.GameId,
                Name = game.Name,
                Rating = game.Rating,
                ReleaseDate = game.ReleaseDate,
                ImageUrl = game.ImageUrl,
                Publisher = game.Publisher,
                SystemRequirements = game.SystemRequirements,
                GameReviews = game.GameReviews.Select(r => new GameReviewViewDTO
                {
                    UserId = r.UserId,
                    Score = r.Score,
                    Review = r.Review,
                    RatedAt = r.RatedAt
                }).ToList(),
                Genres = game.Genres.Select(g => new GenreDTO
                {
                    Name = g.Name,
                    Description = g.Description,
                }).ToList(),
                PublisherName = game.PublisherName,
                MostOneTimePlayers  = game.MostOneTimePlayers,
                Tags = game.Tags,
                Description = game.Desription
            };
        }

        public Task<IReadOnlyCollection<Game>> GetGamesAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<List<GameViewDTO>> GetGamesByGenresAsync(string genre)
        {
            
            var filteredGames = await 
                _gameStore.Games
                .Where(game => game.Genres.Any(g => g.Name == genre))
                .ToListAsync();

            var filteredGamesDto = _mapper.Map<List<GameViewDTO>>(filteredGames);

            return filteredGamesDto;
        }
    }
}
