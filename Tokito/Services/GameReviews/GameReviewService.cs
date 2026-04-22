
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Tokito.Data;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Models;
using Tokito.Services.Games;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.GameReviews
{
    public class GameReviewService : IGameReviewService
    {
        GameStore _gameStore;
        public GameReviewService(GameStore gameStore)
        {
            _gameStore = gameStore;
        }
        public async Task<List<GameReviewViewDTO>> GetGameReviewsById(int id)
        {
            return await _gameStore.GameReviews
                .AsNoTracking()
                .Where(r => r.GameId == id)
                .Select(review => new GameReviewViewDTO
                {
                    UserId = review.UserId,
                    UserName = review.User.UserNickname,
                    Score = review.Score,
                    Review = review.Review,
                    RatedAt = review.RatedAt
                })
                .ToListAsync();
        }
        public async Task<ReviewResult> PostGameReviewById(int userId, int gameId, CreateReviewDto dto)
        {
            var userExists = await _gameStore.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
            {
                return ReviewResult.Failure(ReviewStatus.UserNotFound, "User account was not found.");
            }

            var game = await _gameStore.Games
                .Include(currentGame => currentGame.GameReviews)
                .SingleOrDefaultAsync(currentGame => currentGame.GameId == gameId);

            if (game == null)
            {
                return ReviewResult.Failure(ReviewStatus.GameNotFound, "Game was not found.");
            }

            var isOwnedByUser = await IsOwnedByUserAsync(userId, gameId);
            if (!isOwnedByUser)
            {
                return ReviewResult.Failure(ReviewStatus.GameNotOwned, "Only users who own this game can review it.");
            }

            var userHasReviewedGame = game.GameReviews
                .Any(review => review.UserId == userId);

            if (userHasReviewedGame)
            {
                return ReviewResult.Failure(ReviewStatus.AlreadyReviewed, "User has already reviewed this game!");
            }

            var newReview = new GameReview
            {
                UserId = userId,
                GameId = gameId,
                Score = dto.Score,
                Review = dto.Review,
                RatedAt = DateTime.UtcNow
            };

            game.GameReviews.Add(newReview);
            game.Rating = GameRatingCalculator.Calculate(game.GameReviews.Select(review => review.Score));

            await _gameStore.SaveChangesAsync();
            return ReviewResult.Success();
        }
        public async Task<bool> IsOwnedByUserAsync(int userId, int gameId)
        {
            return await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .SelectMany(user => user.Games)
                .AnyAsync(game => game.GameId == gameId);
        }
    }
}
