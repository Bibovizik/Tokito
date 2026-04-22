using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Models;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.Games
{
    public class GameService : IGameService
    {
        private const string BaseCurrencyCode = "UAH";
        private const string BaseCurrencySymbol = "\u20B4";

        private readonly IMapper _mapper;
        private readonly GameStore _gameStore;

        public GameService(GameStore gameStore, IMapper mapper)
        {
            _mapper = mapper;
            _gameStore = gameStore;
        }

        public async Task<ReviewResult> AddReviewAsync(int userId, int gameId, CreateReviewDto dto)
        {
            var UserHasReviewedGame = _gameStore.GameReviews.Where(gr => gr.UserId == userId && gr.GameId == gameId).Any();
            if (UserHasReviewedGame)
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

            _gameStore.GameReviews.Add(newReview);
            await _gameStore.SaveChangesAsync();
            return ReviewResult.Success();
        }

        public async Task<GameViewDTO?> GetGameByIdAsync(int id, int? userId = null, string? countryCode = null)
        {
            var game = await _gameStore.Games
                .AsNoTracking()
                .Include(g => g.Genres)
                .Include(g => g.GameReviews)
                .Include(g => g.Tags)
                .FirstOrDefaultAsync(g => g.GameId == id);

            if (game == null)
            {
                return null;
            }

            var dto = _mapper.Map<GameViewDTO>(game);
            dto.CurrentPrice = await ResolveGamePriceDtoAsync(game, countryCode);
            dto.WalletPrice = await ResolveWalletPriceDtoAsync(game, userId, dto.CurrentPrice);

            if (userId.HasValue)
            {
                dto.IsOwnedByCurrentUser = await _gameStore.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId.Value)
                    .SelectMany(u => u.Games)
                    .AnyAsync(g => g.GameId == id);
            }

            return dto;
        }

        public async Task<List<GameViewDTO>> GetGamesByGenresAsync(string? genre)
        {
            var query = _gameStore.Games
                .AsNoTracking()
                .Include(g => g.Genres)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(game => game.Genres.Any(g => g.Name == genre));
            }

            var games = await query.ToListAsync();

            return _mapper.Map<List<GameViewDTO>>(games);
        }

        public async Task<PurchaseGameResult> PurchaseGameAsync(int userId, int gameId)
        {
            await using var dbTransaction = await _gameStore.Database.BeginTransactionAsync();

            try
            {
                var user = await _gameStore.Users
                    .Include(u => u.Games)
                    .SingleOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return PurchaseGameResult.Failure(PurchaseGameStatus.UserNotFound, "User account was not found.");
                }

                var game = await _gameStore.Games.SingleOrDefaultAsync(g => g.GameId == gameId);

                if (game == null)
                {
                    return PurchaseGameResult.Failure(PurchaseGameStatus.GameNotFound, "Game was not found.");
                }

                if (user.Games.Any(g => g.GameId == gameId))
                {
                    return PurchaseGameResult.Failure(PurchaseGameStatus.AlreadyOwned, "You already own this game.");
                }

                var storefrontPrice = await ResolvePriceAsync(game, user.CountryCode);
                var walletCurrencyCode = await ResolveWalletCurrencyCodeAsync(user.CountryCode);
                var walletChargePrice = await ResolveWalletChargePriceAsync(game, walletCurrencyCode, storefrontPrice);

                if (walletChargePrice == null)
                {
                    return PurchaseGameResult.Failure(
                        PurchaseGameStatus.PurchaseConflict,
                        $"This account wallet uses {walletCurrencyCode}, but no wallet charge price is available for this game.");
                }

                var walletBalance = await _gameStore.WalletBalances
                    .SingleOrDefaultAsync(b => b.UserId == userId);

                if (walletBalance == null || walletBalance.AvailableAmount < walletChargePrice.Amount)
                {
                    return PurchaseGameResult.Failure(
                        PurchaseGameStatus.InsufficientFunds,
                        $"Insufficient {walletCurrencyCode} balance for this purchase.");
                }

                walletBalance.AvailableAmount -= walletChargePrice.Amount;
                var purchaseDate = DateTime.UtcNow;

                var transaction = new Transaction
                {
                    UserId = userId,
                    GameId = gameId,
                    PurchaseDate = purchaseDate,
                    AmountPaid = walletChargePrice.Amount,
                    CurrencyCode = walletChargePrice.CurrencyCode,
                    BasePriceUahSnapshot = game.BasePriceUah,
                    ExchangeRateSnapshot = walletChargePrice.CurrencyCode == BaseCurrencyCode ? 1m : null,
                    PriceSource = walletChargePrice.Source,
                    RegionId = walletChargePrice.RegionId
                };

                var walletEntry = new WalletEntry
                {
                    UserId = userId,
                    CurrencyCode = walletChargePrice.CurrencyCode,
                    Amount = -walletChargePrice.Amount,
                    BalanceAfter = walletBalance.AvailableAmount,
                    EntryType = WalletEntryType.Purchase,
                    CreatedAt = transaction.PurchaseDate,
                    Transaction = transaction,
                    Description = $"Purchased {game.Name}",
                    ExchangeRateToUahSnapshot = walletChargePrice.CurrencyCode == BaseCurrencyCode ? 1m : null,
                    AmountUahSnapshot = walletChargePrice.CurrencyCode == BaseCurrencyCode ? walletChargePrice.Amount : null
                };

                user.Games.Add(game);
                _gameStore.Transactions.Add(transaction);
                _gameStore.WalletEntries.Add(walletEntry);

                await _gameStore.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return PurchaseGameResult.Success(new PurchaseReceiptDto
                {
                    GameId = game.GameId,
                    GameName = game.Name,
                    PurchasedAt = transaction.PurchaseDate,
                    ChargedPrice = MapPriceDto(walletChargePrice),
                    RemainingBalance = walletBalance.AvailableAmount
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await dbTransaction.RollbackAsync();
                return PurchaseGameResult.Failure(
                    PurchaseGameStatus.ConcurrencyConflict,
                    "Wallet balance changed during checkout. Retry the purchase.");
            }
            catch (DbUpdateException)
            {
                await dbTransaction.RollbackAsync();
                return PurchaseGameResult.Failure(
                    PurchaseGameStatus.PurchaseConflict,
                    "Purchase could not be completed because the game ownership or wallet state changed.");
            }
        }

        private async Task<GamePriceDto> ResolveGamePriceDtoAsync(Game game, string? countryCode)
        {
            var resolvedPrice = await ResolvePriceAsync(game, countryCode);
            return MapPriceDto(resolvedPrice);
        }

        private async Task<GamePriceDto?> ResolveWalletPriceDtoAsync(Game game, int? userId, GamePriceDto? currentPrice)
        {
            if (!userId.HasValue)
            {
                return currentPrice ?? MapPriceDto(CreateBasePrice(game, "WalletBasePriceUah"));
            }

            var countryCode = await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId.Value)
                .Select(user => user.CountryCode)
                .SingleOrDefaultAsync();
            var walletCurrencyCode = await ResolveWalletCurrencyCodeAsync(countryCode);

            var storefrontPrice = currentPrice == null
                ? null
                : new ResolvedGamePrice(
                    currentPrice.Amount,
                    currentPrice.CurrencyCode,
                    currentPrice.CurrencySymbol,
                    currentPrice.Source,
                    null);

            var walletPrice = await ResolveWalletChargePriceAsync(game, walletCurrencyCode, storefrontPrice);
            return walletPrice == null ? null : MapPriceDto(walletPrice);
        }

        private async Task<ResolvedGamePrice> ResolvePriceAsync(Game game, string? countryCode)
        {
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
                var region = await _gameStore.Regions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.CountryCode == normalizedCountryCode && r.IsSupported);

                if (region != null)
                {
                    var regionalPrice = await _gameStore.RegionalPrices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(rp => rp.GameId == game.GameId && rp.RegionId == region.RegionId && rp.IsActive);

                    if (regionalPrice != null)
                    {
                        return new ResolvedGamePrice(
                            regionalPrice.Amount,
                            region.CurrencyCode,
                            region.CurrencySymbol,
                            "RegionalPrice",
                            region.RegionId);
                    }
                }
            }

            return CreateBasePrice(game, "BasePriceUah");
        }

        private async Task<ResolvedGamePrice?> ResolveWalletChargePriceAsync(
            Game game,
            string walletCurrencyCode,
            ResolvedGamePrice? storefrontPrice)
        {
            if (storefrontPrice != null &&
                string.Equals(storefrontPrice.CurrencyCode, walletCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return storefrontPrice;
            }

            return await ResolvePriceByCurrencyAsync(game, walletCurrencyCode);
        }

        private async Task<ResolvedGamePrice?> ResolvePriceByCurrencyAsync(Game game, string currencyCode)
        {
            var normalizedCurrencyCode = currencyCode.Trim().ToUpperInvariant();

            if (normalizedCurrencyCode == BaseCurrencyCode)
            {
                return CreateBasePrice(game, "WalletBasePriceUah");
            }

            var regionalPrice = await _gameStore.RegionalPrices
                .AsNoTracking()
                .Where(price =>
                    price.GameId == game.GameId &&
                    price.IsActive &&
                    price.Region.IsSupported &&
                    price.Region.CurrencyCode == normalizedCurrencyCode)
                .OrderBy(price => price.RegionId)
                .Select(price => new
                {
                    price.Amount,
                    price.RegionId,
                    price.Region.CurrencyCode,
                    price.Region.CurrencySymbol
                })
                .FirstOrDefaultAsync();

            return regionalPrice == null
                ? null
                : new ResolvedGamePrice(
                    regionalPrice.Amount,
                    regionalPrice.CurrencyCode,
                    regionalPrice.CurrencySymbol,
                    "WalletRegionalPrice",
                    regionalPrice.RegionId);
        }

        private async Task<string> ResolveWalletCurrencyCodeAsync(string? countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return BaseCurrencyCode;
            }

            var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
            var walletCurrencyCode = await _gameStore.Regions
                .AsNoTracking()
                .Where(region => region.CountryCode == normalizedCountryCode && region.IsSupported)
                .Select(region => region.CurrencyCode)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(walletCurrencyCode)
                ? BaseCurrencyCode
                : walletCurrencyCode.Trim().ToUpperInvariant();
        }

        private static ResolvedGamePrice CreateBasePrice(Game game, string source)
        {
            return new ResolvedGamePrice(
                game.BasePriceUah,
                BaseCurrencyCode,
                BaseCurrencySymbol,
                source,
                null);
        }

        private static GamePriceDto MapPriceDto(ResolvedGamePrice resolvedPrice)
        {
            return new GamePriceDto
            {
                Amount = resolvedPrice.Amount,
                CurrencyCode = resolvedPrice.CurrencyCode,
                CurrencySymbol = resolvedPrice.CurrencySymbol,
                Source = resolvedPrice.Source
            };
        }

        public async Task<DeleteGameResult> DeleteGameByIdAsync(int gameId, int publisherId, bool isUserAdmin)
        {
            var gameToBeDeleted = await _gameStore.Games
                .FirstOrDefaultAsync(g => g.GameId == gameId);

            if (gameToBeDeleted == null)
            {
                return DeleteGameResult.Failure(DeleteGameStatus.GameNotFound, "Game not found.");
            }

            // 2. Check Permissions
            // If not an admin, the PublisherId MUST match the owner of the game
            if (!isUserAdmin && gameToBeDeleted.PublisherId != publisherId)
            {
                return DeleteGameResult.Failure(DeleteGameStatus.InsufficientRights, "Nuh-uh, you can't delete someone else's game.");
            }

            // 3. Delete
            _gameStore.Games.Remove(gameToBeDeleted);
            await _gameStore.SaveChangesAsync();

            return DeleteGameResult.Success(new DeleteGameResultDto
            {
                DeleteInitializerId = publisherId,
                GameId = gameId
            });
        }

        private sealed record ResolvedGamePrice(
            decimal Amount,
            string CurrencyCode,
            string CurrencySymbol,
            string Source,
            int? RegionId);
    }
}
