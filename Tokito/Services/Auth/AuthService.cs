using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using Tokito.Data;
using Tokito.DTOs.UserDTOs;
using Tokito.Models;
using Tokito.Services.Games;
using Tokito.Services.Markets;
using Tokito.Services.Pricing;

namespace Tokito.Services.Auth
{
    public class AuthService : IAuthService
    {
        private const string BaseCurrencyCode = "UAH";

        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly GameStore _gameStore;
        private readonly IMarketResolver _marketResolver;
        private readonly INbuExchangeRateService _nbuExchangeRateService;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            GameStore gameStore,
            IMarketResolver marketResolver,
            INbuExchangeRateService nbuExchangeRateService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _gameStore = gameStore;
            _marketResolver = marketResolver;
            _nbuExchangeRateService = nbuExchangeRateService;
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

            var normalizedCountryCode = dto.CountryCode.Trim().ToUpperInvariant();
            var rateSnapshot = await ResolveChangeCountryRateSnapshotAsync(userId, normalizedCountryCode);

            try
            {
                var result = await ExecuteChangeCountryProcedureAsync(
                    userId,
                    normalizedCountryCode,
                    rateSnapshot.CurrentCurrencyExchangeRateToUahSnapshot,
                    rateSnapshot.NewCurrencyExchangeRateToUahSnapshot,
                    dto.Description);

                var updatedUser = await _userManager.FindByIdAsync(userId.ToString());
                if (updatedUser == null)
                {
                    throw new KeyNotFoundException("User account was not found.");
                }

                await SignInUserAsync(updatedUser);
                return result;
            }
            catch (SqlException exception) when (exception.Number == 50014)
            {
                throw new KeyNotFoundException("User account was not found.");
            }
            catch (SqlException exception) when (exception.Number is 50015 or 50016)
            {
                throw new ArgumentException(exception.Message, nameof(dto));
            }
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

        private async Task<ChangeCountryResultDto> ExecuteChangeCountryProcedureAsync(
            int userId,
            string normalizedCountryCode,
            decimal? currentCurrencyExchangeRateToUahSnapshot,
            decimal? newCurrencyExchangeRateToUahSnapshot,
            string? description)
        {
            var connection = _gameStore.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_ChangeCountryAndConvertWallet";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(CreateSqlParameter("@UserId", userId));
                command.Parameters.Add(CreateSqlParameter("@NewCountryCode", normalizedCountryCode));
                command.Parameters.Add(CreateSqlParameter("@CurrentCurrencyExchangeRateToUahSnapshot", currentCurrencyExchangeRateToUahSnapshot));
                command.Parameters.Add(CreateSqlParameter("@NewCurrencyExchangeRateToUahSnapshot", newCurrencyExchangeRateToUahSnapshot));
                command.Parameters.Add(CreateSqlParameter("@Description", string.IsNullOrWhiteSpace(description) ? null : description.Trim()));

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException("Stored procedure dbo.usp_ChangeCountryAndConvertWallet did not return a result.");
                }

                return new ChangeCountryResultDto
                {
                    PreviousCountryCode = reader.GetString(reader.GetOrdinal("PreviousCountryCode")),
                    CountryCode = reader.GetString(reader.GetOrdinal("CountryCode")),
                    PreviousCurrencyCode = reader.GetString(reader.GetOrdinal("PreviousCurrencyCode")),
                    CurrencyCode = reader.GetString(reader.GetOrdinal("CurrencyCode")),
                    PreviousBalance = reader.GetDecimal(reader.GetOrdinal("PreviousBalance")),
                    ConvertedBalance = reader.GetDecimal(reader.GetOrdinal("ConvertedBalance")),
                    ChangedAt = reader.GetDateTime(reader.GetOrdinal("ChangedAt")),
                    BalanceWasConverted = reader.GetBoolean(reader.GetOrdinal("BalanceWasConverted"))
                };
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<ChangeCountryRateSnapshot> ResolveChangeCountryRateSnapshotAsync(int userId, string normalizedCountryCode)
        {
            var previousCountryCode = await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.CountryCode)
                .SingleOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(previousCountryCode))
            {
                throw new KeyNotFoundException("User account was not found.");
            }

            var previousCurrencyCode = await _marketResolver.ResolveWalletCurrencyCodeAsync(previousCountryCode);
            var newCurrencyCode = await _marketResolver.ResolveWalletCurrencyCodeAsync(normalizedCountryCode);

            if (string.Equals(previousCurrencyCode, newCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return new ChangeCountryRateSnapshot(null, null);
            }

            var nonBaseCurrencies = new[] { previousCurrencyCode, newCurrencyCode }
                .Where(currencyCode => !string.Equals(currencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            IReadOnlyDictionary<string, ExchangeRateQuote> exchangeRates = new Dictionary<string, ExchangeRateQuote>(StringComparer.OrdinalIgnoreCase);
            if (nonBaseCurrencies.Length > 0)
            {
                try
                {
                    exchangeRates = await _nbuExchangeRateService.GetRatesToUahAsync();
                }
                catch (InvalidOperationException exception)
                {
                    throw new HttpRequestException(exception.Message, exception);
                }
            }

            return new ChangeCountryRateSnapshot(
                ResolveRateToUahSnapshot(previousCurrencyCode, exchangeRates),
                ResolveRateToUahSnapshot(newCurrencyCode, exchangeRates));
        }

        private static SqlParameter CreateSqlParameter(string name, object? value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        private static decimal? ResolveRateToUahSnapshot(
            string currencyCode,
            IReadOnlyDictionary<string, ExchangeRateQuote> exchangeRates)
        {
            if (string.Equals(currencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!exchangeRates.TryGetValue(currencyCode.Trim().ToUpperInvariant(), out var exchangeRateQuote))
            {
                throw new HttpRequestException($"NBU exchange rate for {currencyCode} is unavailable.");
            }

            return exchangeRateQuote.RateToUah;
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

        public async Task<IEnumerable<UserProfileDto>> GetAllProfilesAsync()
        {
            var usersWithPublishers = await _gameStore.Users.AsNoTracking()
                .GroupJoin(
                    _gameStore.Publishers.AsNoTracking(),
                    user => user.Id,
                    pub => pub.UserId,
                    (user, pubGroup) => new { user, pubGroup }
                )
                .SelectMany(
                    x => x.pubGroup.DefaultIfEmpty(),
                    (x, pub) => new
                    {
                        User = x.user,
                        PublisherId = (int?)pub.PublisherId,
                        PublisherName = pub.Name
                    }
                )
                .ToListAsync();

            var rolesLookup = (await _gameStore.UserRoles
                .Join(
                    _gameStore.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, r.Name }
                )
                .ToListAsync())
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToArray());

            return usersWithPublishers.Select(item => new UserProfileDto
            {
                UserId = item.User.Id,
                UserName = item.User.UserName ?? string.Empty,
                UserNickname = item.User.UserNickname,
                Email = item.User.Email,
                CountryCode = item.User.CountryCode,
                RegistrationDate = item.User.RegistrationDate,
                AccountStatusCode = item.User.AccountStatus,
                AccountStatus = ResolveAccountStatusName(item.User.AccountStatus),
                PublisherId = item.PublisherId,
                PublisherName = item.PublisherName,
                Roles = (rolesLookup.TryGetValue(item.User.Id, out var roles) ? roles : Array.Empty<string>())
                            .OrderBy(r => r)
                            .ToArray()
            }).ToList();
        }
        private sealed record ChangeCountryRateSnapshot(
            decimal? CurrentCurrencyExchangeRateToUahSnapshot,
            decimal? NewCurrencyExchangeRateToUahSnapshot);
    }
}
