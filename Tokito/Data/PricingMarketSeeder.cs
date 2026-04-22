using Microsoft.EntityFrameworkCore;
using Tokito.Models;

namespace Tokito.Data
{
    public static class PricingMarketSeeder
    {
        public static async Task SeedAsync(GameStore gameStore)
        {
            var marketSeeds = GetMarketSeeds();
            var regions = await gameStore.Regions
                .Include(region => region.Countries)
                .OrderBy(region => region.RegionId)
                .ToListAsync();

            var configuredRegionIds = new HashSet<int>();

            foreach (var marketSeed in marketSeeds)
            {
                var region = FindExistingRegion(regions, marketSeed);

                if (region == null)
                {
                    region = new Region();
                    gameStore.Regions.Add(region);
                    regions.Add(region);
                }

                region.Code = marketSeed.Code;
                region.Name = marketSeed.Name;
                region.CurrencyCode = marketSeed.CurrencyCode;
                region.CurrencySymbol = marketSeed.CurrencySymbol;
                region.IsSupported = true;

                await gameStore.SaveChangesAsync();

                var existingMappings = await gameStore.RegionCountries
                    .Where(mapping => marketSeed.CountryCodes.Contains(mapping.CountryCode))
                    .ToListAsync();

                foreach (var countryCode in marketSeed.CountryCodes)
                {
                    var existingMapping = existingMappings
                        .FirstOrDefault(mapping => mapping.CountryCode == countryCode);

                    if (existingMapping == null)
                    {
                        gameStore.RegionCountries.Add(new RegionCountry
                        {
                            CountryCode = countryCode,
                            RegionId = region.RegionId
                        });
                    }
                    else
                    {
                        existingMapping.RegionId = region.RegionId;
                    }
                }

                var staleMappings = await gameStore.RegionCountries
                    .Where(mapping =>
                        mapping.RegionId == region.RegionId &&
                        !marketSeed.CountryCodes.Contains(mapping.CountryCode))
                    .ToListAsync();

                if (staleMappings.Count > 0)
                {
                    gameStore.RegionCountries.RemoveRange(staleMappings);
                }

                configuredRegionIds.Add(region.RegionId);
                await gameStore.SaveChangesAsync();
            }

            foreach (var region in regions.Where(region => !configuredRegionIds.Contains(region.RegionId)))
            {
                region.IsSupported = false;
            }

            await gameStore.SaveChangesAsync();
        }

        private static Region? FindExistingRegion(IReadOnlyCollection<Region> regions, PricingMarketSeed marketSeed)
        {
            return regions.FirstOrDefault(region => string.Equals(region.Code, marketSeed.Code, StringComparison.OrdinalIgnoreCase))
                ?? regions.FirstOrDefault(region =>
                    region.Countries.Any(country => marketSeed.CountryCodes.Contains(country.CountryCode)))
                ?? regions.FirstOrDefault(region =>
                    marketSeed.LegacyCodes.Contains(region.Code, StringComparer.OrdinalIgnoreCase));
        }

        private static PricingMarketSeed[] GetMarketSeeds()
        {
            var eurozoneCountryCodes = new[]
            {
                "AT", "BE", "CY", "DE", "EE", "ES", "FI", "FR", "GR", "HR",
                "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PT", "SI", "SK"
            };

            return
            [
                new PricingMarketSeed("UA", "Ukraine", "UAH", "\u20B4", ["UA"], ["UA"]),
                new PricingMarketSeed("US", "United States", "USD", "$", ["US"], ["US"]),
                new PricingMarketSeed("GB", "United Kingdom", "GBP", "\u00A3", ["GB"], ["GB"]),
                new PricingMarketSeed("PL", "Poland", "PLN", "z\u0142", ["PL"], ["PL"]),
                new PricingMarketSeed("EU", "Eurozone", "EUR", "\u20AC", eurozoneCountryCodes, eurozoneCountryCodes),
                new PricingMarketSeed("TR", "Turkey", "TRY", "\u20BA", ["TR"], ["TR"]),
                new PricingMarketSeed("JP", "Japan", "JPY", "\u00A5", ["JP"], ["JP"]),
                new PricingMarketSeed("CA", "Canada", "CAD", "CA$", ["CA"], ["CA"]),
            ];
        }

        private sealed record PricingMarketSeed(
            string Code,
            string Name,
            string CurrencyCode,
            string CurrencySymbol,
            IReadOnlyCollection<string> CountryCodes,
            IReadOnlyCollection<string> LegacyCodes);
    }
}
