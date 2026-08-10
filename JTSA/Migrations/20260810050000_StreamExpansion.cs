using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260810050000_StreamExpansion")]
public partial class StreamExpansion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "T_StreamExpansionHeader",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsRaid = table.Column<bool>(type: "INTEGER", nullable: false),
                IsSubscribe = table.Column<bool>(type: "INTEGER", nullable: false),
                IsBits = table.Column<bool>(type: "INTEGER", nullable: false),
                TriggerComment = table.Column<string>(type: "TEXT", nullable: false),
                TriggerChannelPointId = table.Column<string>(type: "TEXT", nullable: false),
                LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_T_StreamExpansionHeader", x => x.Id));

        migrationBuilder.CreateTable(
            name: "T_StreamExpansionItem",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                HeaderId = table.Column<long>(type: "INTEGER", nullable: false),
                ActionType = table.Column<string>(type: "TEXT", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                Weight = table.Column<int>(type: "INTEGER", nullable: false),
                LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                SortNumber = table.Column<int>(type: "INTEGER", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_T_StreamExpansionItem", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_T_StreamExpansionItem_HeaderId", table: "T_StreamExpansionItem", column: "HeaderId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "T_StreamExpansionItem");
        migrationBuilder.DropTable(name: "T_StreamExpansionHeader");
    }
}
