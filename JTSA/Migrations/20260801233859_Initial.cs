using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "M_CategoryList",
                columns: table => new
                {
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    BoxArtUrl = table.Column<string>(type: "TEXT", nullable: false),
                    SteamUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SteamHeaderArtUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_CategoryList", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "M_FriendList",
                columns: table => new
                {
                    BroadcastId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_FriendList", x => x.BroadcastId);
                });

            migrationBuilder.CreateTable(
                name: "M_SettingList",
                columns: table => new
                {
                    Name = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_SettingList", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "M_TitleTagList",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_TitleTagList", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "T_GamePlaylistHeader",
                columns: table => new
                {
                    GamePlayListId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GamePlayListName = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailCategoryUrl = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_GamePlaylistHeader", x => x.GamePlayListId);
                });

            migrationBuilder.CreateTable(
                name: "T_GamePlaylistItem",
                columns: table => new
                {
                    GamePlayListId = table.Column<long>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_GamePlaylistItem", x => new { x.GamePlayListId, x.CategoryId });
                });

            migrationBuilder.CreateTable(
                name: "T_TitleTextList",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryBoxArtUrl = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_TitleTextList", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "M_CategoryList");

            migrationBuilder.DropTable(
                name: "M_FriendList");

            migrationBuilder.DropTable(
                name: "M_SettingList");

            migrationBuilder.DropTable(
                name: "M_TitleTagList");

            migrationBuilder.DropTable(
                name: "T_GamePlaylistHeader");

            migrationBuilder.DropTable(
                name: "T_GamePlaylistItem");

            migrationBuilder.DropTable(
                name: "T_TitleTextList");
        }
    }
}
