using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.Models;

namespace Tokito.Services.Markets
{
    public class MarketResolver : IMarketResolver
    {
        private const string BaseCurrencyCode = "UAH";

        private readonly GameStore _gameStore;

        public MarketResolver(GameStore gameStore)
        {
            _gameStore = gameStore;
        }

        public async Task<Region?> ResolveSupportedRegionAsync(string? countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return null;
            }

            var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();

            return await _gameStore.Regions
                .AsNoTracking()
                .Where(region =>
                    region.IsSupported &&
                    region.Countries.Any(country => country.CountryCode == normalizedCountryCode))
                .FirstOrDefaultAsync();
        }

        public async Task<string> ResolveWalletCurrencyCodeAsync(string? countryCode)
        {
            var normalizedCountryCode = NormalizeCountryCode(countryCode);

            if (string.IsNullOrWhiteSpace(normalizedCountryCode))
            {
                return BaseCurrencyCode;
            }

            var currencyCode = await _gameStore.Regions
                .AsNoTracking()
                .Where(region =>
                    region.IsSupported &&
                    region.Countries.Any(country => country.CountryCode == normalizedCountryCode))
                .Select(region => region.CurrencyCode)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(currencyCode)
                ? BaseCurrencyCode
                : currencyCode.Trim().ToUpperInvariant();
        }

        private static string NormalizeCountryCode(string? countryCode)
        {
            return string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : countryCode.Trim().ToUpperInvariant();
        }
    }
}
