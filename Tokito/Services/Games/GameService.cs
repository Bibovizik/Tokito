using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.DTOs.Genres;
using Tokito.Models;
using Tokito.Services.Markets;
using Tokito.Services.Pricing;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Services.Games
{
    public class GameService : IGameService
    {
        private const string BaseCurrencyCode = "UAH";
        private const string BaseCurrencySymbol = "\u20B4";

        private static readonly TimeZoneInfo KyivTimeZone = ResolveKyivTimeZone();

        private readonly GameStore _gameStore;
        private readonly IMarketResolver _marketResolver;
        private readonly INbuExchangeRateService _nbuExchangeRateService;

        public GameService(
            GameStore gameStore,
            IMarketResolver marketResolver,
            INbuExchangeRateService nbuExchangeRateService)
        {
            _gameStore = gameStore;
            _marketResolver = marketResolver;
            _nbuExchangeRateService = nbuExchangeRateService;
        }

        public async Task<ReviewResult> AddReviewAsync(int userId, int gameId, CreateReviewDto dto)
        {
            var userHasReviewedGame = await _gameStore.GameReviews
                .AnyAsync(gr => gr.UserId == userId && gr.GameId == gameId);

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
                .Include(g => g.RegionalPrices)
                    .ThenInclude(price => price.Region)
                .FirstOrDefaultAsync(g => g.GameId == id);

            if (game == null)
            {
                return null;
            }

            var effectiveCountryCode = await ResolveEffectiveCountryCodeAsync(userId, countryCode);
            var storefrontRegion = await _marketResolver.ResolveSupportedRegionAsync(effectiveCountryCode);
            var walletCurrencyCode = userId.HasValue
                ? await _marketResolver.ResolveWalletCurrencyCodeAsync(effectiveCountryCode)
                : null;
            var isOwned = userId.HasValue && await IsOwnedByUserAsync(userId.Value, id);

            return MapGameViewDto(game, storefrontRegion, walletCurrencyCode, isOwned, includeReviews: true);
        }

        public async Task<List<GameViewDTO>> GetGamesByGenresAsync(string? genre, int? userId = null, string? countryCode = null)
        {
            var query = _gameStore.Games
                .AsNoTracking()
                .Include(g => g.Genres)
                .Include(g => g.RegionalPrices)
                    .ThenInclude(price => price.Region)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(game => game.Genres.Any(g => g.Name == genre));
            }

            var games = await query
                .OrderBy(game => game.GameId)
                .ToListAsync();

            var effectiveCountryCode = await ResolveEffectiveCountryCodeAsync(userId, countryCode);
            var storefrontRegion = await _marketResolver.ResolveSupportedRegionAsync(effectiveCountryCode);
            var walletCurrencyCode = userId.HasValue
                ? await _marketResolver.ResolveWalletCurrencyCodeAsync(effectiveCountryCode)
                : null;
            var ownedGameIds = userId.HasValue
                ? await GetOwnedGameIdsAsync(userId.Value)
                : new HashSet<int>();

            return games
                .Select(game => MapGameViewDto(
                    game,
                    storefrontRegion,
                    walletCurrencyCode,
                    ownedGameIds.Contains(game.GameId),
                    includeReviews: false))
                .ToList();
        }

        public async Task<CreatedGameDto> CreateGameAsync(int publisherId, CreateGameDto dto, CancellationToken cancellationToken = default)
        {
            var publisher = await _gameStore.Publishers
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.PublisherId == publisherId, cancellationToken);

            if (publisher == null)
            {
                throw new KeyNotFoundException("Publisher account was not found.");
            }

            var genres = await GetGenresAsync(dto.GenreIds, cancellationToken);
            var supportedMarkets = await GetSupportedMarketsAsync(cancellationToken);
            var plannedMarketPrices = await BuildMarketPricesAsync(
                dto.BasePriceUah,
                supportedMarkets,
                dto.MarketPriceOverrides,
                preservedManualPrices: null,
                cancellationToken);

            var game = new Game
            {
                Name = dto.Name.Trim(),
                ReleaseDate = dto.ReleaseDate,
                PublisherId = publisher.PublisherId,
                PublisherName = publisher.Name,
                SystemRequirements = GameJsonSerializer.Serialize(dto.SystemRequirements),
                MostOneTimePlayers = dto.MostOneTimePlayers,
                Desription = dto.Description.Trim(),
                ImageUrl = NormalizeOptionalText(dto.ImageUrl),
                BasePriceUah = dto.BasePriceUah,
                RegionalPrices = plannedMarketPrices
                    .Select(MapRegionalPriceEntity)
                    .ToList()
            };

            foreach (var genre in genres)
            {
                game.Genres.Add(genre);
            }

            _gameStore.Games.Add(game);
            await _gameStore.SaveChangesAsync(cancellationToken);

            return MapCreatedGameDto(game, publisher.Name, genres, plannedMarketPrices);
        }

        public async Task<CreatedGameDto> UpdateGameAsync(int gameId, int publisherId, UpdateGameDto dto, CancellationToken cancellationToken = default)
        {
            var game = await _gameStore.Games
                .Include(g => g.Genres)
                .Include(g => g.RegionalPrices)
                    .ThenInclude(price => price.Region)
                .SingleOrDefaultAsync(g => g.GameId == gameId, cancellationToken);

            if (game == null)
            {
                throw new KeyNotFoundException("Game was not found.");
            }

            if (game.PublisherId != publisherId)
            {
                throw new UnauthorizedAccessException("You can only update your own games.");
            }

            var genres = await GetGenresAsync(dto.GenreIds, cancellationToken);
            var supportedMarkets = await GetSupportedMarketsAsync(cancellationToken);
            var preservedManualPrices = dto.MarketPriceOverrides == null
                ? game.RegionalPrices
                    .Where(price => string.Equals(price.PriceSource, RegionalPrice.ManualOverrideSource, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        price => price.Region.Code.Trim().ToUpperInvariant(),
                        price => new PreservedManualPrice(price.Amount, price.ExchangeRateToUahSnapshot, price.ExchangeDate),
                        StringComparer.OrdinalIgnoreCase)
                : null;

            var plannedMarketPrices = await BuildMarketPricesAsync(
                dto.BasePriceUah,
                supportedMarkets,
                dto.MarketPriceOverrides,
                preservedManualPrices,
                cancellationToken);

            game.Name = dto.Name.Trim();
            game.ReleaseDate = dto.ReleaseDate;
            game.SystemRequirements = GameJsonSerializer.Serialize(dto.SystemRequirements);
            game.MostOneTimePlayers = dto.MostOneTimePlayers;
            game.Desription = dto.Description.Trim();
            game.ImageUrl = NormalizeOptionalText(dto.ImageUrl);
            game.BasePriceUah = dto.BasePriceUah;

            game.Genres.Clear();
            foreach (var genre in genres)
            {
                game.Genres.Add(genre);
            }

            SyncRegionalPrices(game, plannedMarketPrices);

            await _gameStore.SaveChangesAsync(cancellationToken);

            return MapCreatedGameDto(game, game.PublisherName ?? string.Empty, genres, plannedMarketPrices);
        }

        public async Task<GameDashboardDto> GetDashboardAsync(
            int? requestingPublisherId,
            bool isUserAdmin,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            int? gameId,
            int? publisherId,
            CancellationToken cancellationToken = default)
        {
            var (normalizedDateFrom, normalizedDateTo) = NormalizeDateRange(dateFrom, dateTo);
            var effectivePublisherId = ResolveDashboardPublisherScope(requestingPublisherId, isUserAdmin, publisherId);

            if (gameId.HasValue)
            {
                var gameScope = await _gameStore.Games
                    .AsNoTracking()
                    .Where(game => game.GameId == gameId.Value)
                    .Select(game => new { game.GameId, game.PublisherId })
                    .SingleOrDefaultAsync(cancellationToken);

                if (gameScope == null)
                {
                    throw new KeyNotFoundException("Game was not found.");
                }

                if (!isUserAdmin && gameScope.PublisherId != effectivePublisherId)
                {
                    throw new UnauthorizedAccessException("You can only view dashboard data for your own games.");
                }
            }

            var periodStartUtc = ConvertKyivDateToUtc(normalizedDateFrom);
            var periodEndUtcExclusive = ConvertKyivDateToUtc(normalizedDateTo.AddDays(1));

            var transactions = await _gameStore.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.PurchaseDate >= periodStartUtc && transaction.PurchaseDate < periodEndUtcExclusive)
                .Where(transaction => !gameId.HasValue || transaction.GameId == gameId.Value)
                .Where(transaction => !effectivePublisherId.HasValue || transaction.Game.PublisherId == effectivePublisherId.Value)
                .Select(transaction => new DashboardTransactionRow(
                    transaction.GameId,
                    transaction.Game.Name,
                    transaction.PurchaseDate,
                    transaction.AmountPaid,
                    transaction.CurrencyCode,
                    transaction.ExchangeRateSnapshot,
                    transaction.BasePriceUahSnapshot))
                .ToListAsync(cancellationToken);

            var salesRows = transactions
                .Select(transaction => new DashboardSaleRow(
                    transaction.GameId,
                    transaction.GameName,
                    DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(transaction.PurchaseDate, KyivTimeZone)),
                    ResolveRevenueUah(transaction)))
                .ToList();

            var gameSummaries = salesRows
                .GroupBy(row => new { row.GameId, row.GameName })
                .Select(group => new GameDashboardGameSummaryDto
                {
                    GameId = group.Key.GameId,
                    GameName = group.Key.GameName,
                    RevenueUah = Math.Round(group.Sum(row => row.RevenueUah), 2, MidpointRounding.AwayFromZero),
                    CopiesSold = group.Count()
                })
                .OrderByDescending(summary => summary.RevenueUah)
                .ThenBy(summary => summary.GameId)
                .ToList();

            var salesByDate = salesRows
                .GroupBy(row => row.Date)
                .ToDictionary(
                    group => group.Key,
                    group => new
                    {
                        RevenueUah = Math.Round(group.Sum(row => row.RevenueUah), 2, MidpointRounding.AwayFromZero),
                        CopiesSold = group.Count()
                    });

            var daily = new List<GameDashboardDailyPointDto>();
            for (var currentDate = normalizedDateFrom; currentDate <= normalizedDateTo; currentDate = currentDate.AddDays(1))
            {
                salesByDate.TryGetValue(currentDate, out var currentDaySales);

                daily.Add(new GameDashboardDailyPointDto
                {
                    Date = currentDate,
                    RevenueUah = currentDaySales?.RevenueUah ?? 0m,
                    CopiesSold = currentDaySales?.CopiesSold ?? 0
                });
            }

            return new GameDashboardDto
            {
                DateFrom = normalizedDateFrom,
                DateTo = normalizedDateTo,
                PublisherId = effectivePublisherId,
                GameId = gameId,
                Totals = new GameDashboardTotalsDto
                {
                    RevenueUah = Math.Round(gameSummaries.Sum(summary => summary.RevenueUah), 2, MidpointRounding.AwayFromZero),
                    CopiesSold = gameSummaries.Sum(summary => summary.CopiesSold),
                    GameCount = gameSummaries.Count
                },
                Games = gameSummaries,
                Daily = daily
            };
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

                var game = await _gameStore.Games
                    .Include(g => g.RegionalPrices)
                        .ThenInclude(price => price.Region)
                    .SingleOrDefaultAsync(g => g.GameId == gameId);

                if (game == null)
                {
                    return PurchaseGameResult.Failure(PurchaseGameStatus.GameNotFound, "Game was not found.");
                }

                if (user.Games.Any(g => g.GameId == gameId))
                {
                    return PurchaseGameResult.Failure(PurchaseGameStatus.AlreadyOwned, "You already own this game.");
                }

                var storefrontRegion = await _marketResolver.ResolveSupportedRegionAsync(user.CountryCode);
                var storefrontPrice = ResolveStorefrontPrice(game, storefrontRegion);
                var walletCurrencyCode = await _marketResolver.ResolveWalletCurrencyCodeAsync(user.CountryCode);
                var walletChargePrice = ResolveWalletChargePrice(game, walletCurrencyCode, storefrontPrice);

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
                var exchangeRateToUah = walletChargePrice.ExchangeRateToUahSnapshot ?? (walletChargePrice.CurrencyCode == BaseCurrencyCode ? 1m : null);
                var amountUahSnapshot = exchangeRateToUah.HasValue
                    ? Math.Round(walletChargePrice.Amount * exchangeRateToUah.Value, 2, MidpointRounding.AwayFromZero)
                    : game.BasePriceUah;

                var transaction = new Transaction
                {
                    UserId = userId,
                    GameId = gameId,
                    PurchaseDate = purchaseDate,
                    AmountPaid = walletChargePrice.Amount,
                    CurrencyCode = walletChargePrice.CurrencyCode,
                    BasePriceUahSnapshot = game.BasePriceUah,
                    ExchangeRateSnapshot = exchangeRateToUah,
                    PriceSource = walletChargePrice.PriceSource,
                    RegionId = walletChargePrice.RegionId
                };

                var walletEntry = new WalletEntry
                {
                    UserId = userId,
                    CurrencyCode = walletChargePrice.CurrencyCode,
                    Amount = -walletChargePrice.Amount,
                    BalanceAfter = walletBalance.AvailableAmount,
                    EntryType = WalletEntryType.Purchase,
                    CreatedAt = purchaseDate,
                    Transaction = transaction,
                    Description = $"Purchased {game.Name}",
                    ExchangeRateToUahSnapshot = exchangeRateToUah,
                    AmountUahSnapshot = amountUahSnapshot
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
                    PurchasedAt = purchaseDate,
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

        public async Task<DeleteGameResult> DeleteGameByIdAsync(int gameId, int publisherId, bool isUserAdmin)
        {
            var gameToBeDeleted = await _gameStore.Games
                .FirstOrDefaultAsync(g => g.GameId == gameId);

            if (gameToBeDeleted == null)
            {
                return DeleteGameResult.Failure(DeleteGameStatus.GameNotFound, "Game not found.");
            }

            if (!isUserAdmin && gameToBeDeleted.PublisherId != publisherId)
            {
                return DeleteGameResult.Failure(DeleteGameStatus.InsufficientRights, "Nuh-uh, you can't delete someone else's game.");
            }

            _gameStore.Games.Remove(gameToBeDeleted);
            await _gameStore.SaveChangesAsync();

            return DeleteGameResult.Success(new DeleteGameResultDto
            {
                DeleteInitializerId = publisherId,
                GameId = gameId
            });
        }

        private GameViewDTO MapGameViewDto(
            Game game,
            Region? storefrontRegion,
            string? walletCurrencyCode,
            bool isOwnedByCurrentUser,
            bool includeReviews)
        {
            var storefrontPrice = ResolveStorefrontPrice(game, storefrontRegion);
            var walletPrice = ResolveWalletPrice(game, walletCurrencyCode, storefrontPrice);

            return new GameViewDTO
            {
                gameId = game.GameId,
                Name = game.Name,
                ReleaseDate = game.ReleaseDate,
                Rating = game.Rating,
                PublisherId = game.PublisherId,
                SystemRequirements = GameJsonSerializer.Deserialize(game.SystemRequirements),
                MostOneTimePlayers = game.MostOneTimePlayers,
                PublisherName = game.PublisherName,
                Description = game.Desription,
                Genres = game.Genres
                    .OrderBy(genre => genre.Name)
                    .Select(genre => new GenreDTO
                    {
                        Name = genre.Name,
                        Description = genre.Description
                    })
                    .ToList(),
                ImageUrl = game.ImageUrl,
                GameReviews = includeReviews
                    ? game.GameReviews
                        .OrderByDescending(review => review.RatedAt)
                        .Select(review => new GameReviewViewDTO
                        {
                            UserId = review.UserId,
                            Score = review.Score,
                            Review = review.Review,
                            RatedAt = review.RatedAt
                        })
                        .ToList()
                    : [],
                BasePriceUah = game.BasePriceUah,
                CurrentPrice = MapPriceDto(storefrontPrice),
                WalletPrice = walletPrice == null ? null : MapPriceDto(walletPrice),
                IsOwnedByCurrentUser = isOwnedByCurrentUser
            };
        }

        private CreatedGameDto MapCreatedGameDto(
            Game game,
            string publisherName,
            IReadOnlyCollection<Genre> genres,
            IReadOnlyCollection<PlannedMarketPrice> plannedMarketPrices)
        {
            return new CreatedGameDto
            {
                GameId = game.GameId,
                Name = game.Name,
                BasePriceUah = game.BasePriceUah,
                PublisherId = game.PublisherId,
                PublisherName = publisherName,
                ReleaseDate = game.ReleaseDate,
                SystemRequirements = GameJsonSerializer.Deserialize(game.SystemRequirements),
                MostOneTimePlayers = game.MostOneTimePlayers,
                Description = game.Desription,
                ImageUrl = game.ImageUrl,
                Genres = genres
                    .Select(genre => genre.Name)
                    .OrderBy(name => name)
                    .ToList(),
                MarketPrices = plannedMarketPrices
                    .Select(price => new CreatedGameMarketPriceDto
                    {
                        MarketCode = price.MarketCode,
                        MarketName = price.MarketName,
                        CurrencyCode = price.CurrencyCode,
                        CurrencySymbol = price.CurrencySymbol,
                        Amount = price.Amount,
                        Source = price.PriceSource,
                        ExchangeRateToUahSnapshot = price.ExchangeRateToUahSnapshot,
                        ExchangeDate = price.ExchangeDate
                    })
                    .ToList()
            };
        }

        private RegionalPrice MapRegionalPriceEntity(PlannedMarketPrice price)
        {
            return new RegionalPrice
            {
                RegionId = price.RegionId,
                Amount = price.Amount,
                PriceSource = price.PriceSource,
                ExchangeRateToUahSnapshot = price.ExchangeRateToUahSnapshot,
                ExchangeDate = price.ExchangeDate,
                IsActive = true
            };
        }

        private void SyncRegionalPrices(Game game, IReadOnlyCollection<PlannedMarketPrice> plannedMarketPrices)
        {
            var plannedByRegionId = plannedMarketPrices.ToDictionary(price => price.RegionId);

            foreach (var existingPrice in game.RegionalPrices)
            {
                if (plannedByRegionId.TryGetValue(existingPrice.RegionId, out var plannedPrice))
                {
                    existingPrice.Amount = plannedPrice.Amount;
                    existingPrice.PriceSource = plannedPrice.PriceSource;
                    existingPrice.ExchangeRateToUahSnapshot = plannedPrice.ExchangeRateToUahSnapshot;
                    existingPrice.ExchangeDate = plannedPrice.ExchangeDate;
                    existingPrice.IsActive = true;
                }
                else
                {
                    existingPrice.IsActive = false;
                }
            }

            var missingPrices = plannedMarketPrices
                .Where(price => game.RegionalPrices.All(existingPrice => existingPrice.RegionId != price.RegionId))
                .Select(MapRegionalPriceEntity)
                .ToList();

            foreach (var missingPrice in missingPrices)
            {
                game.RegionalPrices.Add(missingPrice);
            }
        }

        private ResolvedGamePrice ResolveStorefrontPrice(Game game, Region? storefrontRegion)
        {
            if (storefrontRegion != null)
            {
                var regionalPrice = game.RegionalPrices
                    .FirstOrDefault(price => price.RegionId == storefrontRegion.RegionId && price.IsActive);

                if (regionalPrice != null)
                {
                    var source = string.Equals(regionalPrice.PriceSource, RegionalPrice.BasePriceSource, StringComparison.OrdinalIgnoreCase)
                        ? RegionalPrice.BasePriceSource
                        : "RegionalPrice";

                    return new ResolvedGamePrice(
                        regionalPrice.Amount,
                        storefrontRegion.CurrencyCode,
                        storefrontRegion.CurrencySymbol,
                        source,
                        regionalPrice.RegionId,
                        ResolveExchangeRateToUahSnapshot(storefrontRegion.CurrencyCode, regionalPrice.ExchangeRateToUahSnapshot),
                        regionalPrice.PriceSource);
                }
            }

            return CreateBasePrice(game, RegionalPrice.BasePriceSource);
        }

        private ResolvedGamePrice? ResolveWalletPrice(Game game, string? walletCurrencyCode, ResolvedGamePrice storefrontPrice)
        {
            if (string.IsNullOrWhiteSpace(walletCurrencyCode))
            {
                return storefrontPrice;
            }

            if (string.Equals(storefrontPrice.CurrencyCode, walletCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return storefrontPrice;
            }

            return ResolvePriceByCurrency(game, walletCurrencyCode);
        }

        private ResolvedGamePrice? ResolveWalletChargePrice(Game game, string walletCurrencyCode, ResolvedGamePrice storefrontPrice)
        {
            if (string.Equals(storefrontPrice.CurrencyCode, walletCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return storefrontPrice;
            }

            return ResolvePriceByCurrency(game, walletCurrencyCode);
        }

        private ResolvedGamePrice? ResolvePriceByCurrency(Game game, string currencyCode)
        {
            var normalizedCurrencyCode = currencyCode.Trim().ToUpperInvariant();

            if (normalizedCurrencyCode == BaseCurrencyCode)
            {
                return CreateBasePrice(game, "WalletBasePriceUah");
            }

            var regionalPrice = game.RegionalPrices
                .Where(price =>
                    price.IsActive &&
                    price.Region.IsSupported &&
                    string.Equals(price.Region.CurrencyCode, normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(price => price.RegionId)
                .FirstOrDefault();

            return regionalPrice == null
                ? null
                : new ResolvedGamePrice(
                    regionalPrice.Amount,
                    regionalPrice.Region.CurrencyCode,
                    regionalPrice.Region.CurrencySymbol,
                    "WalletRegionalPrice",
                    regionalPrice.RegionId,
                    ResolveExchangeRateToUahSnapshot(normalizedCurrencyCode, regionalPrice.ExchangeRateToUahSnapshot),
                    regionalPrice.PriceSource);
        }

        private async Task<IReadOnlyCollection<Genre>> GetGenresAsync(IEnumerable<int> genreIds, CancellationToken cancellationToken)
        {
            var distinctGenreIds = genreIds
                .Distinct()
                .ToList();

            var genres = await _gameStore.Genres
                .Where(genre => distinctGenreIds.Contains(genre.GenreId))
                .ToListAsync(cancellationToken);

            if (genres.Count != distinctGenreIds.Count)
            {
                throw new ArgumentException("One or more genre ids are invalid.", nameof(genreIds));
            }

            return genres;
        }

        private async Task<IReadOnlyCollection<Region>> GetSupportedMarketsAsync(CancellationToken cancellationToken)
        {
            var supportedMarkets = await _gameStore.Regions
                .AsNoTracking()
                .Where(region => region.IsSupported)
                .OrderBy(region => region.RegionId)
                .ToListAsync(cancellationToken);

            if (supportedMarkets.Count == 0)
            {
                throw new InvalidOperationException("No supported pricing markets are configured.");
            }

            return supportedMarkets;
        }

        private async Task<List<PlannedMarketPrice>> BuildMarketPricesAsync(
            decimal basePriceUah,
            IReadOnlyCollection<Region> supportedMarkets,
            IEnumerable<CreateGameMarketPriceOverrideDto>? requestedOverrides,
            IReadOnlyDictionary<string, PreservedManualPrice>? preservedManualPrices,
            CancellationToken cancellationToken)
        {
            var normalizedOverrides = NormalizeOverrides(requestedOverrides);
            var supportedMarketsByCode = supportedMarkets.ToDictionary(
                market => market.Code.Trim().ToUpperInvariant(),
                StringComparer.OrdinalIgnoreCase);

            var unsupportedOverrideCodes = normalizedOverrides.Keys
                .Where(code => !supportedMarketsByCode.ContainsKey(code))
                .OrderBy(code => code)
                .ToList();

            if (unsupportedOverrideCodes.Count > 0)
            {
                throw new ArgumentException(
                    $"Unknown market codes: {string.Join(", ", unsupportedOverrideCodes)}.",
                    nameof(requestedOverrides));
            }

            if (normalizedOverrides.ContainsKey("UA"))
            {
                throw new ArgumentException(
                    "Use BasePriceUah for the Ukraine market. Manual override for UA is not allowed.",
                    nameof(requestedOverrides));
            }

            IReadOnlyDictionary<string, ExchangeRateQuote>? exchangeRates = null;
            var plannedPrices = new List<PlannedMarketPrice>(supportedMarkets.Count);

            foreach (var market in supportedMarkets)
            {
                var marketCode = market.Code.Trim().ToUpperInvariant();
                var currencyCode = market.CurrencyCode.Trim().ToUpperInvariant();

                if (currencyCode == BaseCurrencyCode)
                {
                    plannedPrices.Add(new PlannedMarketPrice(
                        market.RegionId,
                        marketCode,
                        market.Name,
                        currencyCode,
                        market.CurrencySymbol,
                        basePriceUah,
                        RegionalPrice.BasePriceSource,
                        1m,
                        null));
                    continue;
                }

                exchangeRates ??= await _nbuExchangeRateService.GetRatesToUahAsync(cancellationToken);
                if (!exchangeRates.TryGetValue(currencyCode, out var quote))
                {
                    throw new InvalidOperationException($"NBU exchange rate for {currencyCode} is unavailable.");
                }

                if (normalizedOverrides.TryGetValue(marketCode, out var requestedOverride))
                {
                    plannedPrices.Add(new PlannedMarketPrice(
                        market.RegionId,
                        marketCode,
                        market.Name,
                        currencyCode,
                        market.CurrencySymbol,
                        requestedOverride.Amount,
                        RegionalPrice.ManualOverrideSource,
                        quote.RateToUah,
                        quote.ExchangeDate));
                    continue;
                }

                if (preservedManualPrices != null && preservedManualPrices.TryGetValue(marketCode, out var preservedManualPrice))
                {
                    plannedPrices.Add(new PlannedMarketPrice(
                        market.RegionId,
                        marketCode,
                        market.Name,
                        currencyCode,
                        market.CurrencySymbol,
                        preservedManualPrice.Amount,
                        RegionalPrice.ManualOverrideSource,
                        preservedManualPrice.ExchangeRateToUahSnapshot ?? quote.RateToUah,
                        preservedManualPrice.ExchangeDate ?? quote.ExchangeDate));
                    continue;
                }

                plannedPrices.Add(new PlannedMarketPrice(
                    market.RegionId,
                    marketCode,
                    market.Name,
                    currencyCode,
                    market.CurrencySymbol,
                    Math.Round(basePriceUah / quote.RateToUah, 2, MidpointRounding.AwayFromZero),
                    RegionalPrice.AutoExchangeRateSource,
                    quote.RateToUah,
                    quote.ExchangeDate));
            }

            return plannedPrices;
        }

        private static Dictionary<string, CreateGameMarketPriceOverrideDto> NormalizeOverrides(
            IEnumerable<CreateGameMarketPriceOverrideDto>? requestedOverrides)
        {
            if (requestedOverrides == null)
            {
                return new Dictionary<string, CreateGameMarketPriceOverrideDto>(StringComparer.OrdinalIgnoreCase);
            }

            var groupedOverrides = requestedOverrides
                .GroupBy(overridePrice => overridePrice.MarketCode.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var duplicateCodes = groupedOverrides
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(code => code)
                .ToList();

            if (duplicateCodes.Count > 0)
            {
                throw new ArgumentException(
                    $"Duplicate market price overrides were provided for: {string.Join(", ", duplicateCodes)}.",
                    nameof(requestedOverrides));
            }

            return groupedOverrides.ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.OrdinalIgnoreCase);
        }

        private async Task<string?> ResolveEffectiveCountryCodeAsync(int? userId, string? countryCode)
        {
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                return countryCode;
            }

            if (!userId.HasValue)
            {
                return null;
            }

            return await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId.Value)
                .Select(user => user.CountryCode)
                .SingleOrDefaultAsync();
        }

        private async Task<HashSet<int>> GetOwnedGameIdsAsync(int userId)
        {
            var ownedGameIds = await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .SelectMany(user => user.Games)
                .Select(game => game.GameId)
                .ToListAsync();

            return ownedGameIds.ToHashSet();
        }

        private async Task<bool> IsOwnedByUserAsync(int userId, int gameId)
        {
            return await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .SelectMany(user => user.Games)
                .AnyAsync(game => game.GameId == gameId);
        }

        private static decimal? ResolveExchangeRateToUahSnapshot(string currencyCode, decimal? exchangeRateToUahSnapshot)
        {
            if (string.Equals(currencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return 1m;
            }

            return exchangeRateToUahSnapshot;
        }

        private static ResolvedGamePrice CreateBasePrice(Game game, string source)
        {
            return new ResolvedGamePrice(
                game.BasePriceUah,
                BaseCurrencyCode,
                BaseCurrencySymbol,
                source,
                null,
                1m,
                RegionalPrice.BasePriceSource);
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

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static (DateOnly DateFrom, DateOnly DateTo) NormalizeDateRange(DateOnly? dateFrom, DateOnly? dateTo)
        {
            var todayKyiv = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KyivTimeZone));
            var normalizedDateTo = dateTo ?? todayKyiv;
            var normalizedDateFrom = dateFrom ?? normalizedDateTo.AddDays(-6);

            if (normalizedDateFrom > normalizedDateTo)
            {
                throw new ArgumentException("dateFrom must be less than or equal to dateTo.");
            }

            return (normalizedDateFrom, normalizedDateTo);
        }

        private static int? ResolveDashboardPublisherScope(int? requestingPublisherId, bool isUserAdmin, int? publisherId)
        {
            if (isUserAdmin)
            {
                return publisherId;
            }

            if (!requestingPublisherId.HasValue)
            {
                throw new UnauthorizedAccessException("Publisher scope could not be resolved.");
            }

            if (publisherId.HasValue && publisherId.Value != requestingPublisherId.Value)
            {
                throw new UnauthorizedAccessException("You can only view dashboard data for your own publisher account.");
            }

            return requestingPublisherId.Value;
        }

        private static DateTime ConvertKyivDateToUtc(DateOnly date)
        {
            var localDateTime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, KyivTimeZone);
        }

        private static decimal ResolveRevenueUah(DashboardTransactionRow transaction)
        {
            if (string.Equals(transaction.CurrencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return transaction.AmountPaid;
            }

            if (transaction.ExchangeRateSnapshot.HasValue)
            {
                return Math.Round(transaction.AmountPaid * transaction.ExchangeRateSnapshot.Value, 2, MidpointRounding.AwayFromZero);
            }

            return transaction.BasePriceUahSnapshot;
        }

        private static TimeZoneInfo ResolveKyivTimeZone()
        {
            foreach (var timeZoneId in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Utc;
        }

        private sealed record ResolvedGamePrice(
            decimal Amount,
            string CurrencyCode,
            string CurrencySymbol,
            string Source,
            int? RegionId,
            decimal? ExchangeRateToUahSnapshot,
            string PriceSource);

        private sealed record PlannedMarketPrice(
            int RegionId,
            string MarketCode,
            string MarketName,
            string CurrencyCode,
            string CurrencySymbol,
            decimal Amount,
            string PriceSource,
            decimal? ExchangeRateToUahSnapshot,
            DateOnly? ExchangeDate);

        private sealed record PreservedManualPrice(
            decimal Amount,
            decimal? ExchangeRateToUahSnapshot,
            DateOnly? ExchangeDate);

        private sealed record DashboardTransactionRow(
            int GameId,
            string GameName,
            DateTime PurchaseDate,
            decimal AmountPaid,
            string CurrencyCode,
            decimal? ExchangeRateSnapshot,
            decimal BasePriceUahSnapshot);

        private sealed record DashboardSaleRow(
            int GameId,
            string GameName,
            DateOnly Date,
            decimal RevenueUah);
    }
}
