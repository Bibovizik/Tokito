using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class AddedDbSetsForNewTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameReview_Games_GameId",
                table: "GameReview");

            migrationBuilder.DropForeignKey(
                name: "FK_GameReview_Users_UserId",
                table: "GameReview");

            migrationBuilder.DropForeignKey(
                name: "FK_GameTags_Tag_TagsId",
                table: "GameTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tag",
                table: "Tag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameReview",
                table: "GameReview");

            migrationBuilder.RenameTable(
                name: "Tag",
                newName: "Tags");

            migrationBuilder.RenameTable(
                name: "GameReview",
                newName: "GameReviews");

            migrationBuilder.RenameIndex(
                name: "IX_GameReview_GameId",
                table: "GameReviews",
                newName: "IX_GameReviews_GameId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tags",
                table: "Tags",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameReviews",
                table: "GameReviews",
                columns: new[] { "UserId", "GameId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GameReviews_Games_GameId",
                table: "GameReviews",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameReviews_Users_UserId",
                table: "GameReviews",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTags_Tags_TagsId",
                table: "GameTags",
                column: "TagsId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameReviews_Games_GameId",
                table: "GameReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_GameReviews_Users_UserId",
                table: "GameReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_GameTags_Tags_TagsId",
                table: "GameTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tags",
                table: "Tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameReviews",
                table: "GameReviews");

            migrationBuilder.RenameTable(
                name: "Tags",
                newName: "Tag");

            migrationBuilder.RenameTable(
                name: "GameReviews",
                newName: "GameReview");

            migrationBuilder.RenameIndex(
                name: "IX_GameReviews_GameId",
                table: "GameReview",
                newName: "IX_GameReview_GameId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tag",
                table: "Tag",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameReview",
                table: "GameReview",
                columns: new[] { "UserId", "GameId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GameReview_Games_GameId",
                table: "GameReview",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameReview_Users_UserId",
                table: "GameReview",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTags_Tag_TagsId",
                table: "GameTags",
                column: "TagsId",
                principalTable: "Tag",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
