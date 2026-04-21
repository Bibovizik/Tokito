using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStoredWalletCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WalletCurrencyCode",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WalletCurrencyCode",
                table: "Users",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE u
                SET u.WalletCurrencyCode = COALESCE(region.CurrencyCode, 'UAH')
                FROM Users u
                LEFT JOIN Regions region
                    ON region.CountryCode = u.CountryCode
                   AND region.IsSupported = 1;
                """);
        }
    }
}
