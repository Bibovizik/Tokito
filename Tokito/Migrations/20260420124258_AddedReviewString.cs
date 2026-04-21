using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddedReviewString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Review",
                table: "GameReviews",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Review",
                table: "GameReviews");
        }
    }
}
