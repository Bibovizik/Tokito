using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tokito.Data;
using Tokito.DTOs.UserDTOs;
using Tokito.Models;

namespace Tokito.Services.Auth
{
    public class AuthService : IAuthService
    {
        private const string BaseCurrencyCode = "UAH";

        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly GameStore _gameStore;

        public AuthService(UserManager<User> userManager, SignInManager<User> signInManager, GameStore gameStore)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _gameStore = gameStore;
        }

        public async Task<IdentityResult> RegisterUserAsync(UserRegistrationDTO dto)
        {
            var user = new User
            {
                UserName = dto.UserNickname,
                UserNickname = dto.UserNickname,
                Email = dto.Email,
                RegistrationDate = DateOnly.FromDateTime(DateTime.Now),
                AccountStatus = 1,
                CountryCode = dto.CountryCode.Trim().ToUpperInvariant(),
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
            return result;
        }

        public async Task<SignInResult> LoginAsync(UserLoginDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return SignInResult.Failed;

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid) return SignInResult.Failed;

            await SignInUserAsync(user);
            return SignInResult.Success;
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
            var previousCurrencyCode = await ResolveWalletCurrencyCodeAsync(previousCountryCode);
            var newCurrencyCode = await ResolveWalletCurrencyCodeAsync(normalizedCountryCode);

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

        private async Task<string> ResolveWalletCurrencyCodeAsync(string? countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
                return BaseCurrencyCode;

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
    }
}
