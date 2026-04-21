using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class addedUserNicknames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserNickname",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserNickname",
                table: "Users");
        }
    }
}
