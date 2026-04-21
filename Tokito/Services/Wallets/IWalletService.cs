using Tokito.DTOs.WalletDTOs;

namespace Tokito.Services.Wallets
{
    public interface IWalletService
    {
        public Task<WalletSummaryDto> GetWalletAsync(int userId);
        public Task<WalletTopUpResultDto> TopUpAsync(int userId, WalletTopUpDto dto);
    }
}
