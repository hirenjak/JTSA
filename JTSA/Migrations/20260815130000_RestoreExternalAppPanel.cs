using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815130000_RestoreExternalAppPanel")]
public partial class RestoreExternalAppPanel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "T_StreamWindow",
            columns: table => new
            {
                ProcessName = table.Column<string>(type: "TEXT", nullable: false),
                WindowTitle = table.Column<string>(type: "TEXT", nullable: false),
                AppExePath = table.Column<string>(type: "TEXT", nullable: false),
                X = table.Column<int>(type: "INTEGER", nullable: false),
                Y = table.Column<int>(type: "INTEGER", nullable: false),
                Width = table.Column<int>(type: "INTEGER", nullable: false),
                Height = table.Column<int>(type: "INTEGER", nullable: false),
                LastUsedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_T_StreamWindow", x => x.ProcessName));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "T_StreamWindow");
    }
}
