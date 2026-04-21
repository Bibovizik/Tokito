using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWalletCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "Users",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "WalletCurrencyCode",
                table: "Users",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE u
                SET u.WalletCurrencyCode = COALESCE(existingBalance.CurrencyCode, region.CurrencyCode, 'UAH')
                FROM Users u
                OUTER APPLY
                (
                    SELECT TOP (1) wb.CurrencyCode
                    FROM WalletBalances wb
                    WHERE wb.UserId = u.Id
                    ORDER BY
                        CASE WHEN wb.AvailableAmount > 0 THEN 0 ELSE 1 END,
                        wb.WalletBalanceId
                ) existingBalance
                LEFT JOIN Regions region
                    ON region.CountryCode = u.CountryCode
                   AND region.IsSupported = 1
                """);

            migrationBuilder.AlterColumn<string>(
                name: "WalletCurrencyCode",
                table: "Users",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WalletCurrencyCode",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);
        }
    }
}
