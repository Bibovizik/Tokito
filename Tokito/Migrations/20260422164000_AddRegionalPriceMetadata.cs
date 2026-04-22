using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionalPriceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExchangeDate",
                table: "RegionalPrices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToUahSnapshot",
                table: "RegionalPrices",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceSource",
                table: "RegionalPrices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ManualOverride");

            migrationBuilder.Sql(
                """
                UPDATE rp
                SET
                    PriceSource = CASE
                        WHEN r.CurrencyCode = 'UAH' THEN 'BasePriceUah'
                        ELSE 'ManualOverride'
                    END,
                    ExchangeRateToUahSnapshot = CASE
                        WHEN r.CurrencyCode = 'UAH' THEN 1
                        WHEN rp.Amount > 0 THEN ROUND(g.BasePriceUah / rp.Amount, 6)
                        ELSE NULL
                    END,
                    ExchangeDate = CASE
                        WHEN r.CurrencyCode = 'UAH' THEN NULL
                        ELSE CONVERT(date, SYSUTCDATETIME())
                    END
                FROM RegionalPrices rp
                INNER JOIN Regions r ON r.RegionId = rp.RegionId
                INNER JOIN Games g ON g.GameId = rp.GameId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeDate",
                table: "RegionalPrices");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToUahSnapshot",
                table: "RegionalPrices");

            migrationBuilder.DropColumn(
                name: "PriceSource",
                table: "RegionalPrices");
        }
    }
}
