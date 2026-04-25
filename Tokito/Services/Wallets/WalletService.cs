using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.DTOs.WalletDTOs;
using Tokito.Models;
using Tokito.Services.Markets;
using Tokito.Services.Pricing;

namespace Tokito.Services.Wallets
{
    public class WalletService : IWalletService
    {
        private const string BaseCurrencyCode = "UAH";

        private readonly GameStore _gameStore;
        private readonly IMarketResolver _marketResolver;
        private readonly INbuExchangeRateService _nbuExchangeRateService;

        public WalletService(
            GameStore gameStore,
            IMarketResolver marketResolver,
            INbuExchangeRateService nbuExchangeRateService)
        {
            _gameStore = gameStore;
            _marketResolver = marketResolver;
            _nbuExchangeRateService = nbuExchangeRateService;
        }

        public async Task<WalletSummaryDto> GetWalletAsync(int userId)
        {
            var walletCurrencyCode = await GetWalletCurrencyCodeAsync(userId);
            var balance = await _gameStore.WalletBalances
                .AsNoTracking()
                .SingleOrDefaultAsync(b => b.UserId == userId);

            var entries = await _gameStore.WalletEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(20)
                .Select(e => new WalletEntryDto
                {
                    WalletEntryId = e.WalletEntryId,
                    CurrencyCode = e.CurrencyCode,
                    Amount = e.Amount,
                    BalanceAfter = e.BalanceAfter,
                    EntryType = e.EntryType.ToString(),
                    CreatedAt = e.CreatedAt,
                    Description = e.Description,
                    TransactionId = e.TransactionId
                })
                .ToListAsync();

            return new WalletSummaryDto
            {
                Balances =
                [
                    new WalletBalanceDto
                    {
                        CurrencyCode = walletCurrencyCode,
                        AvailableAmount = balance?.AvailableAmount ?? 0m
                    }
                ],
                Entries = entries
            };
        }

        public async Task<WalletTopUpResultDto> TopUpAsync(int userId, WalletTopUpDto dto)
        {
            if (dto.Amount < 0.01m || dto.Amount > 100_000m)
                throw new ArgumentOutOfRangeException(nameof(dto.Amount), "Amount must be between 0.01 and 100000.");

            var walletCurrencyCode = await GetWalletCurrencyCodeAsync(userId);
            var exchangeRateToUahSnapshot = await ResolveWalletExchangeRateToUahSnapshotAsync(walletCurrencyCode);
            var createdAt = DateTime.UtcNow;

            await using var transaction = await _gameStore.Database.BeginTransactionAsync();

            var balance = await _gameStore.WalletBalances
                .SingleOrDefaultAsync(b => b.UserId == userId);

            if (balance == null)
            {
                balance = new WalletBalance
                {
                    UserId = userId,
                    AvailableAmount = 0m
                };

                _gameStore.WalletBalances.Add(balance);
            }

            balance.AvailableAmount += dto.Amount;

            _gameStore.WalletEntries.Add(new WalletEntry
            {
                UserId = userId,
                CurrencyCode = walletCurrencyCode,
                Amount = dto.Amount,
                BalanceAfter = balance.AvailableAmount,
                EntryType = WalletEntryType.TopUp,
                CreatedAt = createdAt,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? "Manual top-up" : dto.Description.Trim(),
                ExchangeRateToUahSnapshot = exchangeRateToUahSnapshot,
                AmountUahSnapshot = Math.Round(dto.Amount * exchangeRateToUahSnapshot, 2, MidpointRounding.AwayFromZero)
            });

            await _gameStore.SaveChangesAsync();
            await transaction.CommitAsync();

            return new WalletTopUpResultDto
            {
                CurrencyCode = walletCurrencyCode,
                CreditedAmount = dto.Amount,
                AvailableAmount = balance.AvailableAmount,
                CreatedAt = createdAt
            };
        }

        private async Task<string> GetWalletCurrencyCodeAsync(int userId)
        {
            var countryCode = await _gameStore.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.CountryCode)
                .SingleOrDefaultAsync();

            return await _marketResolver.ResolveWalletCurrencyCodeAsync(countryCode);
        }

        private async Task<decimal> ResolveWalletExchangeRateToUahSnapshotAsync(string walletCurrencyCode)
        {
            if (string.Equals(walletCurrencyCode, BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return 1m;
            }

            IReadOnlyDictionary<string, ExchangeRateQuote> exchangeRates;
            try
            {
                exchangeRates = await _nbuExchangeRateService.GetRatesToUahAsync();
            }
            catch (InvalidOperationException exception)
            {
                throw new HttpRequestException(exception.Message, exception);
            }

            if (!exchangeRates.TryGetValue(walletCurrencyCode.Trim().ToUpperInvariant(), out var exchangeRateQuote))
            {
                throw new HttpRequestException($"NBU exchange rate for {walletCurrencyCode} is unavailable.");
            }

            return exchangeRateQuote.RateToUah;
        }
    }
}
