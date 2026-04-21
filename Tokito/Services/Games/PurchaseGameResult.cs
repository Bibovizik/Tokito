using Tokito.DTOs.GameDTOs;

namespace Tokito.Services.Games
{
    public class PurchaseGameResult
    {
        public PurchaseGameStatus Status { get; init; }
        public string? Message { get; init; }
        public PurchaseReceiptDto? Receipt { get; init; }

        public static PurchaseGameResult Success(PurchaseReceiptDto receipt) =>
            new()
            {
                Status = PurchaseGameStatus.Success,
                Receipt = receipt
            };

        public static PurchaseGameResult Failure(PurchaseGameStatus status, string message) =>
            new()
            {
                Status = status,
                Message = message
            };
    }
}
