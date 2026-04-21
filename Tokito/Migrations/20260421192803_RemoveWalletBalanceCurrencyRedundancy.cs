using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWalletBalanceCurrencyRedundancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE wb
                FROM WalletBalances wb
                INNER JOIN Users u ON u.Id = wb.UserId
                WHERE wb.CurrencyCode <> u.WalletCurrencyCode;

                WITH DuplicateBalances AS
                (
                    SELECT
                        WalletBalanceId,
                        ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY WalletBalanceId) AS RowNum
                    FROM WalletBalances
                )
                DELETE FROM DuplicateBalances
                WHERE RowNum > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_WalletBalances_UserId_CurrencyCode",
                table: "WalletBalances");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "WalletBalances");

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalances_UserId",
                table: "WalletBalances",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletBalances_UserId",
                table: "WalletBalances");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "WalletBalances",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE wb
                SET wb.CurrencyCode = u.WalletCurrencyCode
                FROM WalletBalances wb
                INNER JOIN Users u ON u.Id = wb.UserId;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "WalletBalances",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalances_UserId_CurrencyCode",
                table: "WalletBalances",
                columns: new[] { "UserId", "CurrencyCode" },
                unique: true);
        }
    }
}
