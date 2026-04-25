using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseProgrammability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(File.ReadAllText(@"Database/Procedures/usp_PurchaseGame.sql"));
            migrationBuilder.Sql(File.ReadAllText(@"Database/Procedures/usp_ChangeCountryAndConvertWallet.sql"));
            migrationBuilder.Sql(File.ReadAllText(@"Database/Triggers/TR_GameReviews_RecalculateGameRating.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_GameReviews_RecalculateGameRating;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ChangeCountryAndConvertWallet;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PurchaseGame;");
        }
    }
}
