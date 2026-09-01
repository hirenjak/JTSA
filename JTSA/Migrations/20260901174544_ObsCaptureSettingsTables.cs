using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class ObsCaptureSettingsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "M_ObsCaptureSource",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsSubObs = table.Column<bool>(type: "INTEGER", nullable: false),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    IsSelected = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_ObsCaptureSource", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "M_ObsCategoryCaptureRule",
                columns: table => new
                {
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    IsSubObs = table.Column<bool>(type: "INTEGER", nullable: false),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    DestinationValue = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_ObsCategoryCaptureRule", x => x.CategoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_M_ObsCaptureSource_IsSubObs_InputName",
                table: "M_ObsCaptureSource",
                columns: new[] { "IsSubObs", "InputName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "M_ObsCaptureSource");

            migrationBuilder.DropTable(
                name: "M_ObsCategoryCaptureRule");
        }
    }
}
