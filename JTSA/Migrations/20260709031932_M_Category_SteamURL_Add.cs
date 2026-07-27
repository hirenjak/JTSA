using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class M_Category_SteamURL_Add : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SteamHeaderUrl",
                table: "M_CategoryList",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SteamUrl",
                table: "M_CategoryList",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SteamHeaderUrl",
                table: "M_CategoryList");

            migrationBuilder.DropColumn(
                name: "SteamUrl",
                table: "M_CategoryList");
        }
    }
}
