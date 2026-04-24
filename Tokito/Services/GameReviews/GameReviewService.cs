using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.DTOs.Common;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Models;
using Tokito.Services.Games;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.GameReviews
{
    public class GameReviewService : IGameReviewService
    {
        private readonly GameStore _gameStore;

        public GameReviewService(GameStore gameStore)
        {
            _gameStore = gameStore;
        }

        public async Task<List<GameReviewViewDTO>> GetGameReviewsById(int id)
        {
            return await BuildGameReviewQuery(id).ToListAsync();
        }

        public async Task<PagedResultDto<GameReviewViewDTO>> GetGameReviewsByIdPagedAsync(int id, int page, int pageSize)
        {
            var query = BuildGameReviewQuery(id);
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<GameReviewViewDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0
                    ? 0
                    : (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = items
            };
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

        private IQueryable<GameReviewViewDTO> BuildGameReviewQuery(int gameId)
        {
            return _gameStore.GameReviews
                .AsNoTracking()
                .Where(review => review.GameId == gameId)
                .OrderByDescending(review => review.RatedAt)
                .ThenBy(review => review.UserId)
                .Select(review => new GameReviewViewDTO
                {
                    UserId = review.UserId,
                    UserName = review.User.UserNickname,
                    Score = review.Score,
                    Review = review.Review,
                    RatedAt = review.RatedAt
                });
        }
    }
}
