CREATE OR ALTER PROCEDURE dbo.usp_PurchaseGame
    @UserId int,
    @GameId int,
    @AmountPaid decimal(18,2),
    @CurrencyCode nvarchar(3),
    @BasePriceUahSnapshot decimal(18,2),
    @ExchangeRateSnapshot decimal(18,6) = NULL,
    @PriceSource nvarchar(32),
    @RegionId int = NULL,
    @PurchaseDateUtc datetime2(7) = NULL,
    @Description nvarchar(200) = NULL,
    @TransactionId int OUTPUT,
    @RemainingBalance decimal(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @PurchaseDateUtc = COALESCE(@PurchaseDateUtc, SYSUTCDATETIME());
    SET @CurrencyCode = UPPER(LTRIM(RTRIM(@CurrencyCode)));
    SET @PriceSource = LTRIM(RTRIM(@PriceSource));
    SET @Description = COALESCE(NULLIF(LTRIM(RTRIM(@Description)), N''), N'Purchased game');

    BEGIN TRAN;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Users
        WHERE Id = @UserId
          AND AccountStatus = 1
    )
        THROW 50001, 'User account was not found or is blocked.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Games
        WHERE GameId = @GameId
    )
        THROW 50002, 'Game was not found.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.UserLibrary WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @UserId
          AND GameId = @GameId
    )
        THROW 50003, 'You already own this game.', 1;

    DECLARE @AvailableAmount decimal(18,2);

    SELECT @AvailableAmount = wb.AvailableAmount
    FROM dbo.WalletBalances wb WITH (UPDLOCK, ROWLOCK)
    WHERE wb.UserId = @UserId;

    IF @AvailableAmount IS NULL OR @AvailableAmount < @AmountPaid
        THROW 50004, 'Insufficient wallet balance.', 1;

    SET @RemainingBalance = @AvailableAmount - @AmountPaid;

    UPDATE dbo.WalletBalances
    SET AvailableAmount = @RemainingBalance
    WHERE UserId = @UserId;

    INSERT INTO dbo.Transactions
    (
        UserId,
        GameId,
        PurchaseDate,
        AmountPaid,
        CurrencyCode,
        BasePriceUahSnapshot,
        ExchangeRateSnapshot,
        PriceSource,
        RegionId
    )
    VALUES
    (
        @UserId,
        @GameId,
        @PurchaseDateUtc,
        @AmountPaid,
        @CurrencyCode,
        @BasePriceUahSnapshot,
        @ExchangeRateSnapshot,
        @PriceSource,
        @RegionId
    );

    SET @TransactionId = CAST(SCOPE_IDENTITY() AS int);

    INSERT INTO dbo.WalletEntries
    (
        UserId,
        CurrencyCode,
        Amount,
        BalanceAfter,
        EntryType,
        CreatedAt,
        Description,
        TransactionId,
        ExchangeRateToUahSnapshot,
        AmountUahSnapshot
    )
    VALUES
    (
        @UserId,
        @CurrencyCode,
        -@AmountPaid,
        @RemainingBalance,
        2,
        @PurchaseDateUtc,
        @Description,
        @TransactionId,
        CASE WHEN @CurrencyCode = N'UAH' THEN 1 ELSE @ExchangeRateSnapshot END,
        CASE
            WHEN @CurrencyCode = N'UAH' THEN @AmountPaid
            WHEN @ExchangeRateSnapshot IS NOT NULL THEN ROUND(@AmountPaid * @ExchangeRateSnapshot, 2)
            ELSE @BasePriceUahSnapshot
        END
    );

    INSERT INTO dbo.UserLibrary (UserId, GameId)
    VALUES (@UserId, @GameId);

    COMMIT TRAN;
END;
GO
