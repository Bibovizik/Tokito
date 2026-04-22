using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tokito.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRegionsToPricingMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CountryCode",
                table: "Regions",
                newName: "Code");

            migrationBuilder.RenameIndex(
                name: "IX_Regions_CountryCode",
                table: "Regions",
                newName: "IX_Regions_Code");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Regions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.CreateTable(
                name: "RegionCountries",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RegionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionCountries", x => x.CountryCode);
                    table.ForeignKey(
                        name: "FK_RegionCountries_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "RegionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegionCountries_RegionId",
                table: "RegionCountries",
                column: "RegionId");

            migrationBuilder.Sql(
                """
                INSERT INTO RegionCountries (CountryCode, RegionId)
                SELECT Code, RegionId
                FROM Regions
                WHERE LEN(LTRIM(RTRIM(Code))) > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegionCountries");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Regions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.RenameIndex(
                name: "IX_Regions_Code",
                table: "Regions",
                newName: "IX_Regions_CountryCode");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Regions",
                newName: "CountryCode");
        }
    }
}
