using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class channel_point_preset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChannelPointPresetId",
                table: "M_Category",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "M_ChannelPoint",
                columns: table => new
                {
                    RewardId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsManageable = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_ChannelPoint", x => x.RewardId);
                });

            migrationBuilder.CreateTable(
                name: "T_ChannelPointPresetHeader",
                columns: table => new
                {
                    PresetId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PresetName = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_ChannelPointPresetHeader", x => x.PresetId);
                });

            migrationBuilder.CreateTable(
                name: "T_ChannelPointPresetItem",
                columns: table => new
                {
                    PresetId = table.Column<long>(type: "INTEGER", nullable: false),
                    RewardId = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RewardTitle = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_ChannelPointPresetItem", x => new { x.PresetId, x.RewardId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "M_ChannelPoint");

            migrationBuilder.DropTable(
                name: "T_ChannelPointPresetHeader");

            migrationBuilder.DropTable(
                name: "T_ChannelPointPresetItem");

            migrationBuilder.DropColumn(
                name: "ChannelPointPresetId",
                table: "M_Category");
        }
    }
}
