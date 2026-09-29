using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260924130000_StreamExpansionFolders")]
public partial class StreamExpansionFolders : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "T_StreamExpansionFolder",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_T_StreamExpansionFolder", x => x.Id));
        migrationBuilder.AddColumn<long>(name: "FolderId", table: "T_StreamExpansionHeader",
            type: "INTEGER", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FolderId", table: "T_StreamExpansionHeader");
        migrationBuilder.DropTable(name: "T_StreamExpansionFolder");
    }
}
