using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260816120000_DailyChatUserCount")]
public partial class DailyChatUserCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "T_DailyChatUserCount",
            columns: table => new
            {
                ChatDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                LoginId = table.Column<string>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                ChatCount = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_T_DailyChatUserCount", x => new { x.ChatDate, x.UserId });
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "T_DailyChatUserCount");
    }
}
