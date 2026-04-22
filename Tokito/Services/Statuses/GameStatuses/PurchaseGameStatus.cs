namespace Tokito.Services.Statuses.GameStatuses
{
    public enum PurchaseGameStatus
    {
        Success = 1,
        GameNotFound = 2,
        UserNotFound = 3,
        AlreadyOwned = 4,
        InsufficientFunds = 5,
        ConcurrencyConflict = 6,
        PurchaseConflict = 7
    }
}
