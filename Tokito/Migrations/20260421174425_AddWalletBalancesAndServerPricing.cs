using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletBalancesAndServerPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegionalPrices_GameId",
                table: "RegionalPrices");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRateSnapshot",
                table: "Transactions",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Transactions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "PriceSource",
                table: "Transactions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<int>(
                name: "RegionId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Regions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencySymbol",
                table: "Regions",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Regions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "Regions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "WalletBalances",
                columns: table => new
                {
                    WalletBalanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AvailableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletBalances", x => x.WalletBalanceId);
                    table.ForeignKey(
                        name: "FK_WalletBalances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletEntries",
                columns: table => new
                {
                    WalletEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EntryType = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransactionId = table.Column<int>(type: "int", nullable: true),
                    ExchangeRateToUahSnapshot = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    AmountUahSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletEntries", x => x.WalletEntryId);
                    table.ForeignKey(
                        name: "FK_WalletEntries_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "TransactionId");
                    table.ForeignKey(
                        name: "FK_WalletEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RegionId",
                table: "Transactions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_CountryCode",
                table: "Regions",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegionalPrices_GameId_RegionId",
                table: "RegionalPrices",
                columns: new[] { "GameId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalances_UserId_CurrencyCode",
                table: "WalletBalances",
                columns: new[] { "UserId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletEntries_TransactionId",
                table: "WalletEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletEntries_UserId",
                table: "WalletEntries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Regions_RegionId",
                table: "Transactions",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "RegionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Regions_RegionId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "WalletBalances");

            migrationBuilder.DropTable(
                name: "WalletEntries");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_RegionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Regions_CountryCode",
                table: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_RegionalPrices_GameId_RegionId",
                table: "RegionalPrices");

            migrationBuilder.DropColumn(
                name: "PriceSource",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Transactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRateSnapshot",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Regions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencySymbol",
                table: "Regions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Regions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "Regions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.CreateIndex(
                name: "IX_RegionalPrices_GameId",
                table: "RegionalPrices",
                column: "GameId");
        }
    }
}
