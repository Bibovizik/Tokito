using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddedDescriptionToGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Desription",
                table: "Games",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Desription",
                table: "Games");
        }
    }
}
