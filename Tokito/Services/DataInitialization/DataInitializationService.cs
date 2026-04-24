using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tokito.Data;
using Tokito.DTOs.AdminDTOs;
using Tokito.Models;
using Tokito.Services.Games;

namespace Tokito.Services.DataInitialization
{
    public class DataInitializationService : IDataInitializationService
    {
        private const string BaseCurrencyCode = "UAH";
        private const string DefaultSeedFilePath = "Data/Seed/initial-catalog.json";
        private const string ImagesDirectoryName = "images";
        private const string SeedUserPassword = "TokitoSeedPassw0rd!";

        private static readonly DateOnly SeedExchangeDate = new(2026, 4, 1);
        private static readonly string[] SeedCountryCodes =
        [
            "UA", "US", "GB", "PL", "DE", "TR", "JP", "CA", "FR", "ES"
        ];

        private static readonly string[] ReviewTemplates =
        [
            "Strong core loop and clean onboarding.",
            "Solid production quality for a catalog seed build.",
            "Worth the price and easy to recommend.",
            "Good regional pricing and stable performance.",
            "Clear progression and a polished first session.",
            "Useful as a demo title with believable data."
        ];

        private static readonly Dictionary<string, decimal> SeedRatesToUah = new(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 39.50m,
            ["GBP"] = 50.80m,
            ["PLN"] = 10.25m,
            ["EUR"] = 43.20m,
            ["TRY"] = 1.17m,
            ["JPY"] = 0.27m,
            ["CAD"] = 28.90m
        };

        private readonly GameStore _gameStore;
        private readonly UserManager<User> _userManager;
        private readonly IHostEnvironment _hostEnvironment;

        public DataInitializationService(
            GameStore gameStore,
            UserManager<User> userManager,
            IHostEnvironment hostEnvironment)
        {
            _gameStore = gameStore;
            _userManager = userManager;
            _hostEnvironment = hostEnvironment;
        }

        public async Task<DataInitializationResultDto> InitializeAsync(
            DataInitializationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var seedFilePath = ResolveSeedFilePath(request.SeedFilePath);
            var seedCatalog = await LoadSeedCatalogAsync(seedFilePath, cancellationToken);
            var result = new DataInitializationResultDto
            {
                SeedFilePath = seedFilePath
            };

            var supportedRegions = await _gameStore.Regions
                .AsNoTracking()
                .Where(region => region.IsSupported)
                .OrderBy(region => region.RegionId)
                .ToListAsync(cancellationToken);

            if (supportedRegions.Count == 0)
            {
                throw new InvalidOperationException("Supported pricing markets must be configured before data initialization.");
            }

            var genresByName = await EnsureGenresAsync(seedCatalog.Genres, result, cancellationToken);
            await EnsurePublishersAndGamesAsync(seedCatalog.Publishers, genresByName, supportedRegions, result, cancellationToken);
            var playerUsers = await EnsurePlayerUsersAsync(request.TestUserCount, result, cancellationToken);
            await SeedPlayerActivityAsync(
                playerUsers,
                request.MaxPurchasesPerUser,
                request.MaxReviewsPerUser,
                result,
                cancellationToken);

            return result;
        }

        private async Task<Dictionary<string, Genre>> EnsureGenresAsync(
            IReadOnlyCollection<SeedGenreDto> seedGenres,
            DataInitializationResultDto result,
            CancellationToken cancellationToken)
        {
            var existingGenres = await _gameStore.Genres
                .ToListAsync(cancellationToken);

            var existingByName = existingGenres.ToDictionary(
                genre => NormalizeKey(genre.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var seedGenre in seedGenres)
            {
                var genreKey = NormalizeKey(seedGenre.Name);
                if (existingByName.ContainsKey(genreKey))
                {
                    continue;
                }

                var genre = new Genre
                {
                    Name = seedGenre.Name.Trim(),
                    Description = NormalizeOptionalText(seedGenre.Description)
                };

                _gameStore.Genres.Add(genre);
                existingByName[genreKey] = genre;
                result.GenresCreated++;
            }

            if (result.GenresCreated > 0)
            {
                await _gameStore.SaveChangesAsync(cancellationToken);
            }

            return await _gameStore.Genres
                .ToDictionaryAsync(
                    genre => NormalizeKey(genre.Name),
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);
        }

        private async Task EnsurePublishersAndGamesAsync(
            IReadOnlyCollection<SeedPublisherDto> seedPublishers,
            IReadOnlyDictionary<string, Genre> genresByName,
            IReadOnlyCollection<Region> supportedRegions,
            DataInitializationResultDto result,
            CancellationToken cancellationToken)
        {
            foreach (var seedPublisher in seedPublishers)
            {
                var normalizedEmail = seedPublisher.Email.Trim();
                var user = await _userManager.FindByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    user = new User
                    {
                        UserName = seedPublisher.UserNickname.Trim(),
                        UserNickname = seedPublisher.UserNickname.Trim(),
                        Email = normalizedEmail,
                        RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        AccountStatus = UserAccountStatusValues.Active,
                        CountryCode = seedPublisher.CountryCode.Trim().ToUpperInvariant()
                    };

                    var createResult = await _userManager.CreateAsync(user, SeedUserPassword);
                    EnsureIdentitySucceeded(createResult, $"publisher user {normalizedEmail}");
                    result.PublisherUsersCreated++;
                }
                else
                {
                    result.SkippedExistingPublisherUsers++;
                }

                await EnsureRolesAsync(user, ["User", "Publisher"]);

                var publisher = await _gameStore.Publishers
                    .SingleOrDefaultAsync(currentPublisher => currentPublisher.UserId == user.Id, cancellationToken);

                if (publisher == null)
                {
                    publisher = new Publisher
                    {
                        UserId = user.Id,
                        Name = seedPublisher.PublisherName.Trim(),
                        FoundationDate = seedPublisher.FoundationDate,
                        Website = NormalizeOptionalText(seedPublisher.Website),
                        CountryCode = seedPublisher.CountryCode.Trim().ToUpperInvariant()
                    };

                    _gameStore.Publishers.Add(publisher);
                    await _gameStore.SaveChangesAsync(cancellationToken);
                    result.PublishersCreated++;
                }
                else
                {
                    result.SkippedExistingPublishers++;
                }

                var existingGameNames = await _gameStore.Games
                    .AsNoTracking()
                    .Where(game => game.PublisherId == publisher.PublisherId)
                    .Select(game => game.Name)
                    .ToListAsync(cancellationToken);

                var existingGameNamesSet = existingGameNames
                    .Select(NormalizeKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var seedGame in seedPublisher.Games)
                {
                    if (existingGameNamesSet.Contains(NormalizeKey(seedGame.Name)))
                    {
                        result.SkippedExistingGames++;
                        continue;
                    }

                    var game = new Game
                    {
                        Name = seedGame.Name.Trim(),
                        ReleaseDate = seedGame.ReleaseDate,
                        PublisherId = publisher.PublisherId,
                        PublisherName = publisher.Name,
                        SystemRequirements = GameJsonSerializer.Serialize(seedGame.SystemRequirements),
                        MostOneTimePlayers = seedGame.MostOneTimePlayers,
                        Desription = seedGame.Description.Trim(),
                        ImageUrl = ResolveSeedImageUrl(seedGame.ImageUrl),
                        BasePriceUah = seedGame.BasePriceUah,
                        RegionalPrices = BuildRegionalPrices(
                            seedGame.BasePriceUah,
                            supportedRegions,
                            seedGame.MarketPriceOverrides)
                    };

                    foreach (var genreName in seedGame.GenreNames
                        .Select(NormalizeKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (!genresByName.TryGetValue(genreName, out var genre))
                        {
                            throw new InvalidOperationException($"Seed game '{seedGame.Name}' references unknown genre '{genreName}'.");
                        }

                        game.Genres.Add(genre);
                    }

                    _gameStore.Games.Add(game);
                    existingGameNamesSet.Add(NormalizeKey(seedGame.Name));
                    result.GamesCreated++;
                }
            }

            if (result.GamesCreated > 0)
            {
                await _gameStore.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<List<User>> EnsurePlayerUsersAsync(
            int testUserCount,
            DataInitializationResultDto result,
            CancellationToken cancellationToken)
        {
            var generatedEmails = new List<string>(testUserCount);

            for (var index = 1; index <= testUserCount; index++)
            {
                var email = $"seed.user{index:000}@tokito.local";
                generatedEmails.Add(email);

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    var countryCode = SeedCountryCodes[(index - 1) % SeedCountryCodes.Length];
                    user = new User
                    {
                        UserName = $"seed-user-{index:000}",
                        UserNickname = $"seed-user-{index:000}",
                        Email = email,
                        RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        AccountStatus = UserAccountStatusValues.Active,
                        CountryCode = countryCode
                    };

                    var createResult = await _userManager.CreateAsync(user, SeedUserPassword);
                    EnsureIdentitySucceeded(createResult, $"player user {email}");
                    result.PlayerUsersCreated++;
                }
                else
                {
                    result.SkippedExistingPlayerUsers++;
                }

                await EnsureRolesAsync(user, ["User"]);
            }

            return await _gameStore.Users
                .Where(user => user.Email != null && generatedEmails.Contains(user.Email))
                .OrderBy(user => user.Email)
                .ToListAsync(cancellationToken);
        }

        private async Task SeedPlayerActivityAsync(
            IReadOnlyCollection<User> playerUsers,
            int maxPurchasesPerUser,
            int maxReviewsPerUser,
            DataInitializationResultDto result,
            CancellationToken cancellationToken)
        {
            if (maxPurchasesPerUser <= 0 || playerUsers.Count == 0)
            {
                return;
            }

            var playerUserIds = playerUsers
                .Select(user => user.Id)
                .ToList();

            var usersWithExistingActivity = await _gameStore.Transactions
                .AsNoTracking()
                .Where(transaction => playerUserIds.Contains(transaction.UserId))
                .Select(transaction => transaction.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var usersWithExistingWalletEntries = await _gameStore.WalletEntries
                .AsNoTracking()
                .Where(entry => playerUserIds.Contains(entry.UserId))
                .Select(entry => entry.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var busyUserIds = usersWithExistingActivity
                .Concat(usersWithExistingWalletEntries)
                .ToHashSet();

            var targetUsers = await _gameStore.Users
                .Include(user => user.Games)
                .Where(user => playerUserIds.Contains(user.Id) && !busyUserIds.Contains(user.Id))
                .OrderBy(user => user.Id)
                .ToListAsync(cancellationToken);

            if (targetUsers.Count == 0)
            {
                return;
            }

            var games = await _gameStore.Games
                .Include(game => game.RegionalPrices)
                    .ThenInclude(price => price.Region)
                .OrderBy(game => game.GameId)
                .ToListAsync(cancellationToken);

            if (games.Count == 0)
            {
                return;
            }

            var supportedRegions = await _gameStore.Regions
                .AsNoTracking()
                .Include(region => region.Countries)
                .Where(region => region.IsSupported)
                .OrderBy(region => region.RegionId)
                .ToListAsync(cancellationToken);

            var walletBalancesByUserId = await _gameStore.WalletBalances
                .Where(balance => playerUserIds.Contains(balance.UserId))
                .ToDictionaryAsync(balance => balance.UserId, cancellationToken);

            var random = new Random(20260423);
            var affectedGameIds = new HashSet<int>();

            foreach (var user in targetUsers)
            {
                var purchasePlans = BuildPurchasePlansForUser(
                    user,
                    games,
                    supportedRegions,
                    maxPurchasesPerUser,
                    random);

                if (purchasePlans.Count == 0)
                {
                    continue;
                }

                var walletCurrencyCode = ResolveWalletCurrencyCode(user.CountryCode, supportedRegions);
                var walletBalance = EnsureWalletBalance(user.Id, walletBalancesByUserId);
                var topUpAmount = Math.Round(
                    purchasePlans.Sum(plan => plan.Price.Amount) * 1.35m + 25m,
                    2,
                    MidpointRounding.AwayFromZero);

                walletBalance.AvailableAmount += topUpAmount;
                var firstPurchaseAt = purchasePlans.Min(plan => plan.PurchaseDate);
                _gameStore.WalletEntries.Add(new WalletEntry
                {
                    UserId = user.Id,
                    CurrencyCode = walletCurrencyCode,
                    Amount = topUpAmount,
                    BalanceAfter = walletBalance.AvailableAmount,
                    EntryType = WalletEntryType.TopUp,
                    CreatedAt = firstPurchaseAt.AddMinutes(-30),
                    Description = "Seeded wallet top-up",
                    ExchangeRateToUahSnapshot = ResolveExchangeRateToUahSnapshot(walletCurrencyCode),
                    AmountUahSnapshot = Math.Round(
                        topUpAmount * ResolveExchangeRateToUahSnapshot(walletCurrencyCode),
                        2,
                        MidpointRounding.AwayFromZero)
                });

                result.WalletsSeeded++;

                foreach (var plan in purchasePlans.OrderBy(plan => plan.PurchaseDate))
                {
                    walletBalance.AvailableAmount -= plan.Price.Amount;

                    var transaction = new Transaction
                    {
                        UserId = user.Id,
                        GameId = plan.Game.GameId,
                        PurchaseDate = plan.PurchaseDate,
                        AmountPaid = plan.Price.Amount,
                        CurrencyCode = plan.Price.CurrencyCode,
                        BasePriceUahSnapshot = plan.Game.BasePriceUah,
                        ExchangeRateSnapshot = plan.Price.ExchangeRateToUahSnapshot,
                        PriceSource = plan.Price.PriceSource,
                        RegionId = plan.Price.RegionId
                    };

                    user.Games.Add(plan.Game);
                    _gameStore.Transactions.Add(transaction);
                    _gameStore.WalletEntries.Add(new WalletEntry
                    {
                        UserId = user.Id,
                        CurrencyCode = plan.Price.CurrencyCode,
                        Amount = -plan.Price.Amount,
                        BalanceAfter = walletBalance.AvailableAmount,
                        EntryType = WalletEntryType.Purchase,
                        CreatedAt = plan.PurchaseDate,
                        Description = $"Seeded purchase of {plan.Game.Name}",
                        Transaction = transaction,
                        ExchangeRateToUahSnapshot = plan.Price.ExchangeRateToUahSnapshot,
                        AmountUahSnapshot = Math.Round(
                            plan.Price.Amount * plan.Price.ExchangeRateToUahSnapshot,
                            2,
                            MidpointRounding.AwayFromZero)
                    });

                    result.TransactionsCreated++;
                }

                var reviewsToCreate = Math.Min(maxReviewsPerUser, purchasePlans.Count);
                if (reviewsToCreate <= 0)
                {
                    continue;
                }

                var reviewCount = random.Next(0, reviewsToCreate + 1);
                foreach (var reviewPlan in purchasePlans
                    .OrderBy(_ => random.Next())
                    .Take(reviewCount))
                {
                    _gameStore.GameReviews.Add(new GameReview
                    {
                        UserId = user.Id,
                        GameId = reviewPlan.Game.GameId,
                        Score = random.Next(6, 11),
                        Review = ReviewTemplates[random.Next(ReviewTemplates.Length)],
                        RatedAt = reviewPlan.PurchaseDate.AddHours(random.Next(6, 120))
                    });

                    affectedGameIds.Add(reviewPlan.Game.GameId);
                    result.ReviewsCreated++;
                }
            }

            await _gameStore.SaveChangesAsync(cancellationToken);
            await RecalculateGameRatingsAsync(affectedGameIds, cancellationToken);
        }

        private WalletBalance EnsureWalletBalance(int userId, IDictionary<int, WalletBalance> walletBalancesByUserId)
        {
            if (walletBalancesByUserId.TryGetValue(userId, out var walletBalance))
            {
                return walletBalance;
            }

            walletBalance = new WalletBalance
            {
                UserId = userId,
                AvailableAmount = 0m
            };

            _gameStore.WalletBalances.Add(walletBalance);
            walletBalancesByUserId[userId] = walletBalance;
            return walletBalance;
        }

        private List<PurchasePlan> BuildPurchasePlansForUser(
            User user,
            IReadOnlyCollection<Game> games,
            IReadOnlyCollection<Region> supportedRegions,
            int maxPurchasesPerUser,
            Random random)
        {
            var availableGames = games
                .OrderBy(_ => random.Next())
                .ToList();

            var requestedCount = Math.Min(random.Next(1, maxPurchasesPerUser + 1), availableGames.Count);
            var purchasePlans = new List<PurchasePlan>(requestedCount);

            for (var index = 0; index < requestedCount; index++)
            {
                var game = availableGames[index];
                var resolvedPrice = ResolvePurchasePrice(game, user.CountryCode, supportedRegions);
                if (resolvedPrice == null)
                {
                    continue;
                }

                purchasePlans.Add(new PurchasePlan(
                    game,
                    resolvedPrice,
                    DateTime.UtcNow.AddDays(-random.Next(1, 60)).AddMinutes(-random.Next(0, 1440))));
            }

            return purchasePlans;
        }

        private SeedResolvedPrice? ResolvePurchasePrice(
            Game game,
            string? countryCode,
            IReadOnlyCollection<Region> supportedRegions)
        {
            var normalizedCountryCode = NormalizeKey(countryCode);
            var storefrontRegion = supportedRegions
                .FirstOrDefault(region => region.Countries.Any(country => NormalizeKey(country.CountryCode) == normalizedCountryCode));
            var walletCurrencyCode = storefrontRegion?.CurrencyCode?.Trim().ToUpperInvariant() ?? BaseCurrencyCode;

            var walletCurrencyPrice = game.RegionalPrices
                .Where(price =>
                    price.IsActive &&
                    price.Region.IsSupported &&
                    string.Equals(price.Region.CurrencyCode, walletCurrencyCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(price => price.RegionId)
                .FirstOrDefault();

            if (walletCurrencyPrice != null)
            {
                return new SeedResolvedPrice(
                    walletCurrencyPrice.Amount,
                    walletCurrencyPrice.Region.CurrencyCode,
                    ResolveExchangeRateToUahSnapshot(walletCurrencyPrice.Region.CurrencyCode),
                    walletCurrencyPrice.PriceSource,
                    walletCurrencyPrice.RegionId);
            }

            var storefrontPrice = storefrontRegion == null
                ? null
                : game.RegionalPrices.FirstOrDefault(price => price.IsActive && price.RegionId == storefrontRegion.RegionId);

            if (storefrontPrice != null)
            {
                return new SeedResolvedPrice(
                    storefrontPrice.Amount,
                    storefrontRegion!.CurrencyCode,
                    ResolveExchangeRateToUahSnapshot(storefrontRegion.CurrencyCode),
                    storefrontPrice.PriceSource,
                    storefrontRegion.RegionId);
            }

            return new SeedResolvedPrice(
                game.BasePriceUah,
                BaseCurrencyCode,
                1m,
                RegionalPrice.BasePriceSource,
                null);
        }

        private string ResolveWalletCurrencyCode(string? countryCode, IReadOnlyCollection<Region> supportedRegions)
        {
            var normalizedCountryCode = NormalizeKey(countryCode);
            var region = supportedRegions
                .FirstOrDefault(currentRegion => currentRegion.Countries.Any(country => NormalizeKey(country.CountryCode) == normalizedCountryCode));

            return region?.CurrencyCode?.Trim().ToUpperInvariant() ?? BaseCurrencyCode;
        }

        private List<RegionalPrice> BuildRegionalPrices(
            decimal basePriceUah,
            IReadOnlyCollection<Region> supportedRegions,
            IReadOnlyCollection<SeedMarketPriceOverrideDto> marketPriceOverrides)
        {
            var overridesByCode = marketPriceOverrides
                .GroupBy(overridePrice => NormalizeKey(overridePrice.MarketCode), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Amount,
                    StringComparer.OrdinalIgnoreCase);

            var prices = new List<RegionalPrice>(supportedRegions.Count);

            foreach (var region in supportedRegions.OrderBy(currentRegion => currentRegion.RegionId))
            {
                var currencyCode = region.CurrencyCode.Trim().ToUpperInvariant();
                if (currencyCode == BaseCurrencyCode)
                {
                    prices.Add(new RegionalPrice
                    {
                        RegionId = region.RegionId,
                        Amount = basePriceUah,
                        PriceSource = RegionalPrice.BasePriceSource,
                        ExchangeRateToUahSnapshot = 1m,
                        ExchangeDate = null,
                        IsActive = true
                    });
                    continue;
                }

                if (!SeedRatesToUah.TryGetValue(currencyCode, out var exchangeRateToUah))
                {
                    throw new InvalidOperationException($"No deterministic seed exchange rate configured for {currencyCode}.");
                }

                var regionCode = NormalizeKey(region.Code);
                var isManualOverride = overridesByCode.TryGetValue(regionCode, out var overrideAmount);
                prices.Add(new RegionalPrice
                {
                    RegionId = region.RegionId,
                    Amount = isManualOverride
                        ? overrideAmount
                        : Math.Round(basePriceUah / exchangeRateToUah, 2, MidpointRounding.AwayFromZero),
                    PriceSource = isManualOverride
                        ? RegionalPrice.ManualOverrideSource
                        : RegionalPrice.AutoExchangeRateSource,
                    ExchangeRateToUahSnapshot = exchangeRateToUah,
                    ExchangeDate = SeedExchangeDate,
                    IsActive = true
                });
            }

            return prices;
        }

        private async Task<SeedCatalogDto> LoadSeedCatalogAsync(string seedFilePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(seedFilePath))
            {
                throw new FileNotFoundException("Seed file was not found.", seedFilePath);
            }

            await using var stream = File.OpenRead(seedFilePath);
            var seedCatalog = await JsonSerializer.DeserializeAsync<SeedCatalogDto>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            if (seedCatalog == null)
            {
                throw new InvalidOperationException("Seed file is empty or invalid.");
            }

            return seedCatalog;
        }

        private string ResolveSeedFilePath(string? requestedPath)
        {
            var relativeOrAbsolutePath = string.IsNullOrWhiteSpace(requestedPath)
                ? DefaultSeedFilePath
                : requestedPath.Trim();

            return Path.IsPathRooted(relativeOrAbsolutePath)
                ? relativeOrAbsolutePath
                : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, relativeOrAbsolutePath));
        }

        private string? ResolveSeedImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            var trimmedImageUrl = imageUrl.Trim();
            var normalizedRelativePath = NormalizeSeedImageRelativePath(trimmedImageUrl);
            var webRootPath = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
            var imagesRootPath = Path.GetFullPath(Path.Combine(webRootPath, ImagesDirectoryName));
            var candidatePath = Path.GetFullPath(Path.Combine(webRootPath, normalizedRelativePath));
            var imagesPrefix = imagesRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!candidatePath.StartsWith(imagesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Seed image '{imageUrl}' must resolve inside wwwroot/{ImagesDirectoryName}.");
            }

            if (!File.Exists(candidatePath))
            {
                throw new InvalidOperationException(
                    $"Seed image '{imageUrl}' was not found at '{candidatePath}'.");
            }

            return "/" + normalizedRelativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string NormalizeSeedImageRelativePath(string imageUrl)
        {
            var normalized = imageUrl
                .TrimStart('~')
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            if (!normalized.Contains(Path.DirectorySeparatorChar))
            {
                normalized = Path.Combine(ImagesDirectoryName, normalized);
            }

            return normalized;
        }

        private async Task EnsureRolesAsync(User user, IReadOnlyCollection<string> roles)
        {
            var existingRoles = await _userManager.GetRolesAsync(user);
            var missingRoles = roles
                .Except(existingRoles, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingRoles.Length == 0)
            {
                return;
            }

            var addRolesResult = await _userManager.AddToRolesAsync(user, missingRoles);
            EnsureIdentitySucceeded(addRolesResult, $"roles for {user.Email}");
        }

        private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
        {
            if (result.Succeeded)
            {
                return;
            }

            var errorMessage = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to create {operation}. {errorMessage}");
        }

        private async Task RecalculateGameRatingsAsync(
            IReadOnlyCollection<int> affectedGameIds,
            CancellationToken cancellationToken)
        {
            if (affectedGameIds.Count == 0)
            {
                return;
            }

            var averageScoresByGameId = await _gameStore.GameReviews
                .AsNoTracking()
                .Where(review => affectedGameIds.Contains(review.GameId))
                .GroupBy(review => review.GameId)
                .Select(group => new
                {
                    GameId = group.Key,
                    AverageScore = group.Average(review => (decimal)review.Score)
                })
                .ToDictionaryAsync(group => group.GameId, group => group.AverageScore, cancellationToken);

            var games = await _gameStore.Games
                .Where(game => affectedGameIds.Contains(game.GameId))
                .ToListAsync(cancellationToken);

            foreach (var game in games)
            {
                game.Rating = averageScoresByGameId.TryGetValue(game.GameId, out var averageScore)
                    ? GameRatingCalculator.Calculate(averageScore)
                    : null;
            }

            await _gameStore.SaveChangesAsync(cancellationToken);
        }

        private static decimal ResolveExchangeRateToUahSnapshot(string currencyCode)
        {
            return string.Equals(currencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase)
                ? 1m
                : SeedRatesToUah.TryGetValue(currencyCode.Trim().ToUpperInvariant(), out var exchangeRateToUah)
                    ? exchangeRateToUah
                    : 1m;
        }

        private static string NormalizeKey(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private sealed record PurchasePlan(Game Game, SeedResolvedPrice Price, DateTime PurchaseDate);

        private sealed record SeedResolvedPrice(
            decimal Amount,
            string CurrencyCode,
            decimal ExchangeRateToUahSnapshot,
            string PriceSource,
            int? RegionId);

        private sealed class SeedCatalogDto
        {
            public List<SeedGenreDto> Genres { get; set; } = [];

            public List<SeedPublisherDto> Publishers { get; set; } = [];
        }

        private sealed class SeedGenreDto
        {
            public string Name { get; set; } = string.Empty;

            public string? Description { get; set; }
        }

        private sealed class SeedPublisherDto
        {
            public string UserNickname { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public string CountryCode { get; set; } = "UA";

            public string PublisherName { get; set; } = string.Empty;

            public DateOnly? FoundationDate { get; set; }

            public string? Website { get; set; }

            public List<SeedGameDto> Games { get; set; } = [];
        }

        private sealed class SeedGameDto
        {
            public string Name { get; set; } = string.Empty;

            public DateOnly? ReleaseDate { get; set; }

            public JsonElement? SystemRequirements { get; set; }

            public int MostOneTimePlayers { get; set; } = 1;

            public string Description { get; set; } = string.Empty;

            public decimal BasePriceUah { get; set; }

            public string? ImageUrl { get; set; }

            public List<string> GenreNames { get; set; } = [];

            public List<SeedMarketPriceOverrideDto> MarketPriceOverrides { get; set; } = [];
        }

        private sealed class SeedMarketPriceOverrideDto
        {
            public string MarketCode { get; set; } = string.Empty;

            public decimal Amount { get; set; }
        }
    }
}
