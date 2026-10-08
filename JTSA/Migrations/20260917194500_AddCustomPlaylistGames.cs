using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260917194500_AddCustomPlaylistGames")]
public partial class AddCustomPlaylistGames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CustomGameName",
            table: "T_GamePlaylistItem",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "CustomImageUrl",
            table: "T_GamePlaylistItem",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IsCustomGame",
            table: "T_GamePlaylistItem",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CustomGameName", table: "T_GamePlaylistItem");
        migrationBuilder.DropColumn(name: "CustomImageUrl", table: "T_GamePlaylistItem");
        migrationBuilder.DropColumn(name: "IsCustomGame", table: "T_GamePlaylistItem");
    }
}
