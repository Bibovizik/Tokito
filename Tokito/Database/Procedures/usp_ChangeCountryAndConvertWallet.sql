CREATE OR ALTER PROCEDURE dbo.usp_ChangeCountryAndConvertWallet
    @UserId int,
    @NewCountryCode nvarchar(3),
    @CurrentCurrencyExchangeRateToUahSnapshot decimal(18,6) = NULL,
    @NewCurrencyExchangeRateToUahSnapshot decimal(18,6) = NULL,
    @Description nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ChangedAtUtc datetime2(7) = SYSUTCDATETIME();
    SET @NewCountryCode = UPPER(LTRIM(RTRIM(@NewCountryCode)));

    IF @NewCountryCode IS NULL OR @NewCountryCode = N''
        THROW 50011, 'Country code is required.', 1;

    IF @CurrentCurrencyExchangeRateToUahSnapshot IS NOT NULL
       AND @CurrentCurrencyExchangeRateToUahSnapshot <= 0
        THROW 50012, 'Current currency exchange rate must be positive.', 1;

    IF @NewCurrencyExchangeRateToUahSnapshot IS NOT NULL
       AND @NewCurrencyExchangeRateToUahSnapshot <= 0
        THROW 50013, 'New currency exchange rate must be positive.', 1;

    BEGIN TRAN;

    DECLARE @PreviousCountryCode nvarchar(3);

    SELECT @PreviousCountryCode = UPPER(LTRIM(RTRIM(u.CountryCode)))
    FROM dbo.Users u WITH (UPDLOCK, HOLDLOCK)
    WHERE u.Id = @UserId;

    IF @PreviousCountryCode IS NULL
        THROW 50014, 'User account was not found.', 1;

    DECLARE @PreviousCurrencyCode nvarchar(3) = N'UAH';
    DECLARE @ResolvedNewCurrencyCode nvarchar(3) = N'UAH';

    SELECT TOP (1) @PreviousCurrencyCode = UPPER(LTRIM(RTRIM(r.CurrencyCode)))
    FROM dbo.RegionCountries rc
    INNER JOIN dbo.Regions r ON r.RegionId = rc.RegionId
    WHERE rc.CountryCode = @PreviousCountryCode
      AND r.IsSupported = 1;

    SELECT TOP (1) @ResolvedNewCurrencyCode = UPPER(LTRIM(RTRIM(r.CurrencyCode)))
    FROM dbo.RegionCountries rc
    INNER JOIN dbo.Regions r ON r.RegionId = rc.RegionId
    WHERE rc.CountryCode = @NewCountryCode
      AND r.IsSupported = 1;

    DECLARE @PreviousBalance decimal(18,2) = 0;
    DECLARE @ConvertedBalance decimal(18,2) = 0;
    DECLARE @BalanceWasConverted bit = 0;

    SELECT @PreviousBalance = wb.AvailableAmount
    FROM dbo.WalletBalances wb WITH (UPDLOCK, ROWLOCK)
    WHERE wb.UserId = @UserId;

    SET @PreviousBalance = COALESCE(@PreviousBalance, 0);
    SET @ConvertedBalance = @PreviousBalance;

    IF @PreviousCurrencyCode <> @ResolvedNewCurrencyCode
       AND @PreviousBalance <> 0
    BEGIN
        DECLARE @PreviousRate decimal(18,6);
        DECLARE @NewRate decimal(18,6);
        DECLARE @AmountUah decimal(18,2);

        IF @PreviousCurrencyCode <> N'UAH' AND @CurrentCurrencyExchangeRateToUahSnapshot IS NULL
            THROW 50015, 'Current currency exchange rate to UAH is required.', 1;

        IF @ResolvedNewCurrencyCode <> N'UAH' AND @NewCurrencyExchangeRateToUahSnapshot IS NULL
            THROW 50016, 'New currency exchange rate to UAH is required.', 1;

        SET @PreviousRate = CASE
            WHEN @PreviousCurrencyCode = N'UAH' THEN 1
            ELSE @CurrentCurrencyExchangeRateToUahSnapshot
        END;

        SET @NewRate = CASE
            WHEN @ResolvedNewCurrencyCode = N'UAH' THEN 1
            ELSE @NewCurrencyExchangeRateToUahSnapshot
        END;

        SET @AmountUah = ROUND(
            CASE
                WHEN @PreviousCurrencyCode = N'UAH' THEN @PreviousBalance
                ELSE @PreviousBalance * @PreviousRate
            END,
            2
        );

        SET @ConvertedBalance = ROUND(
            CASE
                WHEN @ResolvedNewCurrencyCode = N'UAH' THEN @AmountUah
                ELSE @AmountUah / @NewRate
            END,
            2
        );

        UPDATE dbo.WalletBalances
        SET AvailableAmount = @ConvertedBalance
        WHERE UserId = @UserId;

        SET @Description = COALESCE(
            NULLIF(LTRIM(RTRIM(@Description)), N''),
            CONCAT(N'Wallet converted due to country change from ', @PreviousCountryCode, N' to ', @NewCountryCode)
        );

        INSERT INTO dbo.WalletEntries
        (
            UserId,
            CurrencyCode,
            Amount,
            BalanceAfter,
            EntryType,
            CreatedAt,
            Description,
            ExchangeRateToUahSnapshot,
            AmountUahSnapshot
        )
        VALUES
        (
            @UserId,
            @PreviousCurrencyCode,
            -@PreviousBalance,
            0,
            5,
            @ChangedAtUtc,
            @Description,
            @PreviousRate,
            @AmountUah
        );

        INSERT INTO dbo.WalletEntries
        (
            UserId,
            CurrencyCode,
            Amount,
            BalanceAfter,
            EntryType,
            CreatedAt,
            Description,
            ExchangeRateToUahSnapshot,
            AmountUahSnapshot
        )
        VALUES
        (
            @UserId,
            @ResolvedNewCurrencyCode,
            @ConvertedBalance,
            @ConvertedBalance,
            5,
            @ChangedAtUtc,
            @Description,
            @NewRate,
            @AmountUah
        );

        SET @BalanceWasConverted = 1;
    END;

    UPDATE dbo.Users
    SET CountryCode = @NewCountryCode
    WHERE Id = @UserId;

    COMMIT TRAN;

    SELECT
        @PreviousCountryCode AS PreviousCountryCode,
        @NewCountryCode AS CountryCode,
        @PreviousCurrencyCode AS PreviousCurrencyCode,
        @ResolvedNewCurrencyCode AS CurrencyCode,
        @PreviousBalance AS PreviousBalance,
        @ConvertedBalance AS ConvertedBalance,
        @ChangedAtUtc AS ChangedAt,
        @BalanceWasConverted AS BalanceWasConverted;
END;
GO
