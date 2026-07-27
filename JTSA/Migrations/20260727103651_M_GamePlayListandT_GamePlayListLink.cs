using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class M_GamePlayListandT_GamePlayListLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "M_GamePlayList",
                columns: table => new
                {
                    GamePlayListId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GamePlayListName = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailCategoryUrl = table.Column<string>(type: "TEXT", nullable: false),
                    CountSelected = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUseDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_GamePlayList", x => x.GamePlayListId);
                });

            migrationBuilder.CreateTable(
                name: "T_GamePlayListLink",
                columns: table => new
                {
                    GamePlayListId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    CountSelected = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUseDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_GamePlayListLink", x => new { x.GamePlayListId, x.CategoryId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "M_GamePlayList");

            migrationBuilder.DropTable(
                name: "T_GamePlayListLink");
        }
    }
}
