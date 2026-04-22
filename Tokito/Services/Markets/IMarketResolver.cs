using Tokito.Models;

namespace Tokito.Services.Markets
{
    public interface IMarketResolver
    {
        Task<Region?> ResolveSupportedRegionAsync(string? countryCode);

        Task<string> ResolveWalletCurrencyCodeAsync(string? countryCode);
    }
}
