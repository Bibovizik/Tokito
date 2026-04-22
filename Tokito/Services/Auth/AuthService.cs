using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tokito.Data;
using Tokito.DTOs.UserDTOs;
using Tokito.Models;
using Tokito.Services.Games;
using Tokito.Services.Markets;

namespace Tokito.Services.Auth
{
    public class AuthService : IAuthService
    {
        private const string BaseCurrencyCode = "UAH";

        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly GameStore _gameStore;
        private readonly IMarketResolver _marketResolver;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            GameStore gameStore,
            IMarketResolver marketResolver)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _gameStore = gameStore;
            _marketResolver = marketResolver;
        }

        public async Task<IdentityResult> RegisterUserAsync(UserRegistrationDTO dto)
        {
            var duplicateEmailResult = await ValidateUniqueEmailAsync(dto.Email);
            if (duplicateEmailResult != null)
            {
                return duplicateEmailResult;
            }

            var user = new User
            {
                UserName = dto.UserNickname,
                UserNickname = dto.UserNickname,
                Email = dto.Email,
                RegistrationDate = DateOnly.FromDateTime(DateTime.Now),
                AccountStatus = UserAccountStatusValues.Active,
                CountryCode = dto.CountryCode.Trim().ToUpperInvariant(),
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
            return result;
        }

        public async Task<IdentityResult> RegisterPublisherAsync(PublisherRegistrationDTO dto)
        {
            var duplicateEmailResult = await ValidateUniqueEmailAsync(dto.Email);
            if (duplicateEmailResult != null)
            {
                return duplicateEmailResult;
            }

            var normalizedCountryCode = dto.CountryCode.Trim().ToUpperInvariant();

            var user = new User
            {
                UserName = dto.UserNickname,
                UserNickname = dto.UserNickname,
                Email = dto.Email,
                RegistrationDate = DateOnly.FromDateTime(DateTime.Now),
                AccountStatus = UserAccountStatusValues.Active,
                CountryCode = normalizedCountryCode,
            };

            await using var transaction = await _gameStore.Database.BeginTransactionAsync();

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                return result;
            }

            var roleResult = await _userManager.AddToRolesAsync(user, ["User", "Publisher"]);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return roleResult;
            }

            _gameStore.Publishers.Add(new Publisher
            {
                Name = dto.PublisherName.Trim(),
                FoundationDate = dto.FoundationDate,
                Website = string.IsNullOrWhiteSpace(dto.Website) ? null : dto.Website.Trim(),
                CountryCode = normalizedCountryCode,
                UserId = user.Id
            });

            await _gameStore.SaveChangesAsync();
            await transaction.CommitAsync();

            return IdentityResult.Success;
        }

        public async Task<SignInResult> LoginAsync(UserLoginDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return SignInResult.Failed;
            }

            var normalizedEmail = _userManager.NormalizeEmail(dto.Email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return SignInResult.Failed;
            }

            var matchingUsers = await _gameStore.Users
                .Where(currentUser => currentUser.NormalizedEmail == normalizedEmail)
                .OrderBy(currentUser => currentUser.Id)
                .Take(2)
                .ToListAsync();

            if (matchingUsers.Count > 1)
            {
                throw new InvalidOperationException("Multiple accounts share this email address. Remove duplicate users before logging in.");
            }

            var user = matchingUsers.SingleOrDefault();
            if (user == null) return SignInResult.Failed;
            if (user.AccountStatus == UserAccountStatusValues.Blocked) return SignInResult.NotAllowed;

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid) return SignInResult.Failed;

            await SignInUserAsync(user);
            return SignInResult.Success;
        }

        public async Task<UserProfileDto> GetProfileAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new KeyNotFoundException("User account was not found.");
            }

            var publisher = await _gameStore.Publishers
                .AsNoTracking()
                .Where(currentPublisher => currentPublisher.UserId == userId)
                .Select(currentPublisher => new
                {
                    currentPublisher.PublisherId,
                    currentPublisher.Name
                })
                .SingleOrDefaultAsync();

            var roles = await _userManager.GetRolesAsync(user);

            return new UserProfileDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                UserNickname = user.UserNickname,
                Email = user.Email,
                CountryCode = user.CountryCode,
                RegistrationDate = user.RegistrationDate,
                AccountStatusCode = user.AccountStatus,
                AccountStatus = ResolveAccountStatusName(user.AccountStatus),
                PublisherId = publisher?.PublisherId,
                PublisherName = publisher?.Name,
                Roles = roles
                    .OrderBy(role => role)
                    .ToArray()
            };
        }

        public async Task UpdateAccountStatusAsync(int userId, byte accountStatus)
        {
            if (accountStatus is < UserAccountStatusValues.Active or > UserAccountStatusValues.Blocked)
            {
                throw new ArgumentOutOfRangeException(nameof(accountStatus), "Unsupported account status.");
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new KeyNotFoundException("User account was not found.");
            }

            user.AccountStatus = accountStatus;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
            }
        }

        public async Task<DeleteUserResultDto> DeleteUserAsync(int userId)
        {
            await using var transaction = await _gameStore.Database.BeginTransactionAsync();

            var user = await _gameStore.Users
                .Include(currentUser => currentUser.Games)
                .SingleOrDefaultAsync(currentUser => currentUser.Id == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User account was not found.");
            }

            var publisher = await _gameStore.Publishers
                .SingleOrDefaultAsync(currentPublisher => currentPublisher.UserId == userId);

            if (publisher != null)
            {
                var ownsGames = await _gameStore.Games
                    .AnyAsync(game => game.PublisherId == publisher.PublisherId);

                if (ownsGames)
                {
                    throw new InvalidOperationException("Cannot delete a publisher account that still owns games.");
                }
            }

            user.Games.Clear();

            var walletEntries = await _gameStore.WalletEntries
                .Where(entry => entry.UserId == userId)
                .ToListAsync();
            _gameStore.WalletEntries.RemoveRange(walletEntries);

            var transactions = await _gameStore.Transactions
                .Where(currentTransaction => currentTransaction.UserId == userId)
                .ToListAsync();
            _gameStore.Transactions.RemoveRange(transactions);

            var reviews = await _gameStore.GameReviews
                .Where(review => review.UserId == userId)
                .ToListAsync();
            var reviewedGameIds = reviews
                .Select(review => review.GameId)
                .Distinct()
                .ToList();
            _gameStore.GameReviews.RemoveRange(reviews);

            var walletBalances = await _gameStore.WalletBalances
                .Where(balance => balance.UserId == userId)
                .ToListAsync();
            _gameStore.WalletBalances.RemoveRange(walletBalances);

            if (publisher != null)
            {
                _gameStore.Publishers.Remove(publisher);
            }

            await _gameStore.SaveChangesAsync();

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(string.Join(" ", deleteResult.Errors.Select(error => error.Description)));
            }

            await RecalculateGameRatingsAsync(reviewedGameIds);

            await transaction.CommitAsync();

            return new DeleteUserResultDto
            {
                UserId = userId,
                WasPublisherAccount = publisher != null,
                PublisherId = publisher?.PublisherId
            };
        }

        public async Task<ChangeCountryResultDto> ChangeCountryAsync(int userId, ChangeCountryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CountryCode))
                throw new ArgumentException("Country code is required.", nameof(dto.CountryCode));

            ValidateExchangeRate(dto.CurrentCurrencyExchangeRateToUahSnapshot, nameof(dto.CurrentCurrencyExchangeRateToUahSnapshot));
            ValidateExchangeRate(dto.NewCurrencyExchangeRateToUahSnapshot, nameof(dto.NewCurrencyExchangeRateToUahSnapshot));

            var changedAt = DateTime.UtcNow;
            var normalizedCountryCode = dto.CountryCode.Trim().ToUpperInvariant();

            await using var transaction = await _gameStore.Database.BeginTransactionAsync();

            var user = await _gameStore.Users.SingleOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new InvalidOperationException("User account was not found.");

            var previousCountryCode = user.CountryCode.Trim().ToUpperInvariant();
            var previousCurrencyCode = await _marketResolver.ResolveWalletCurrencyCodeAsync(previousCountryCode);
            var newCurrencyCode = await _marketResolver.ResolveWalletCurrencyCodeAsync(normalizedCountryCode);

            var balance = await _gameStore.WalletBalances.SingleOrDefaultAsync(b => b.UserId == userId);
            var previousBalance = balance?.AvailableAmount ?? 0m;
            var convertedBalance = previousBalance;
            var balanceWasConverted = false;

            if (!string.Equals(previousCurrencyCode, newCurrencyCode, StringComparison.OrdinalIgnoreCase) &&
                previousBalance != 0m)
            {
                var previousRateToUah = ResolveRateToUah(previousCurrencyCode, dto.CurrentCurrencyExchangeRateToUahSnapshot, nameof(dto.CurrentCurrencyExchangeRateToUahSnapshot));
                var newRateToUah = ResolveRateToUah(newCurrencyCode, dto.NewCurrencyExchangeRateToUahSnapshot, nameof(dto.NewCurrencyExchangeRateToUahSnapshot));
                var amountUah = previousCurrencyCode == BaseCurrencyCode
                    ? previousBalance
                    : previousBalance * previousRateToUah;

                convertedBalance = newCurrencyCode == BaseCurrencyCode
                    ? amountUah
                    : amountUah / newRateToUah;
                convertedBalance = Math.Round(convertedBalance, 2, MidpointRounding.AwayFromZero);

                balance!.AvailableAmount = convertedBalance;
                balanceWasConverted = true;

                var description = string.IsNullOrWhiteSpace(dto.Description)
                    ? $"Wallet converted due to country change from {previousCountryCode} to {normalizedCountryCode}"
                    : dto.Description.Trim();
                var amountUahSnapshot = Math.Round(amountUah, 2, MidpointRounding.AwayFromZero);

                _gameStore.WalletEntries.Add(new WalletEntry
                {
                    UserId = userId,
                    CurrencyCode = previousCurrencyCode,
                    Amount = -previousBalance,
                    BalanceAfter = 0m,
                    EntryType = WalletEntryType.CurrencyConversion,
                    CreatedAt = changedAt,
                    Description = description,
                    ExchangeRateToUahSnapshot = previousRateToUah,
                    AmountUahSnapshot = amountUahSnapshot
                });

                _gameStore.WalletEntries.Add(new WalletEntry
                {
                    UserId = userId,
                    CurrencyCode = newCurrencyCode,
                    Amount = convertedBalance,
                    BalanceAfter = convertedBalance,
                    EntryType = WalletEntryType.CurrencyConversion,
                    CreatedAt = changedAt,
                    Description = description,
                    ExchangeRateToUahSnapshot = newRateToUah,
                    AmountUahSnapshot = amountUahSnapshot
                });
            }

            user.CountryCode = normalizedCountryCode;

            await _gameStore.SaveChangesAsync();
            await transaction.CommitAsync();
            await SignInUserAsync(user);

            return new ChangeCountryResultDto
            {
                PreviousCountryCode = previousCountryCode,
                CountryCode = normalizedCountryCode,
                PreviousCurrencyCode = previousCurrencyCode,
                CurrencyCode = newCurrencyCode,
                PreviousBalance = previousBalance,
                ConvertedBalance = convertedBalance,
                ChangedAt = changedAt,
                BalanceWasConverted = balanceWasConverted
            };
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }

        private async Task SignInUserAsync(User user)
        {
            // 1. Find if this user is a publisher
            var publisher = await _gameStore.Publishers
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var claims = new List<Claim>
                {
                    new Claim("countryCode", user.CountryCode.Trim().ToUpperInvariant())
                };

            // 2. If they are a publisher, add that ID to their claims
            if (publisher != null)
            {
                claims.Add(new Claim("PublisherId", publisher.PublisherId.ToString()));
                claims.Add(new Claim(ClaimTypes.Role, "Publisher"));
            }

            await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, claims);
        }

        private async Task<IdentityResult?> ValidateUniqueEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var normalizedEmail = _userManager.NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return null;
            }

            var emailAlreadyExists = await _gameStore.Users
                .AsNoTracking()
                .AnyAsync(user => user.NormalizedEmail == normalizedEmail);

            if (!emailAlreadyExists)
            {
                return null;
            }

            return IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.DuplicateEmail),
                Description = "Email is already taken."
            });
        }

        private static void ValidateExchangeRate(decimal? exchangeRate, string paramName)
        {
            if (exchangeRate.HasValue &&
                (exchangeRate.Value < 0.00001m || exchangeRate.Value > 999_999_999m))
            {
                throw new ArgumentOutOfRangeException(paramName, "Exchange rate must be positive.");
            }
        }

        private static decimal ResolveRateToUah(string currencyCode, decimal? exchangeRateToUah, string paramName)
        {
            if (currencyCode == BaseCurrencyCode)
                return 1m;

            return exchangeRateToUah ?? throw new ArgumentException(
                $"Exchange rate to UAH is required for {currencyCode}.",
                paramName);
        }

        private async Task RecalculateGameRatingsAsync(IReadOnlyCollection<int> gameIds)
        {
            if (gameIds.Count == 0)
            {
                return;
            }

            var averageScoresByGameId = await _gameStore.GameReviews
                .AsNoTracking()
                .Where(review => gameIds.Contains(review.GameId))
                .GroupBy(review => review.GameId)
                .Select(group => new
                {
                    GameId = group.Key,
                    AverageScore = group.Average(review => (decimal)review.Score)
                })
                .ToDictionaryAsync(group => group.GameId, group => group.AverageScore);

            var affectedGames = await _gameStore.Games
                .Where(game => gameIds.Contains(game.GameId))
                .ToListAsync();

            foreach (var affectedGame in affectedGames)
            {
                affectedGame.Rating = averageScoresByGameId.TryGetValue(affectedGame.GameId, out var averageScore)
                    ? GameRatingCalculator.Calculate(averageScore)
                    : null;
            }

            await _gameStore.SaveChangesAsync();
        }

        private static string ResolveAccountStatusName(byte accountStatus)
        {
            return accountStatus switch
            {
                UserAccountStatusValues.Active => "Active",
                UserAccountStatusValues.Blocked => "Blocked",
                _ => "Unknown"
            };
        }
    }
}
