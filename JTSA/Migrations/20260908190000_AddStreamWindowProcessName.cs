using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260908190000_AddStreamWindowProcessName")]
public partial class AddStreamWindowProcessName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WindowProcessName",
            table: "T_StreamWindow",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WindowProcessName",
            table: "T_StreamWindow");
    }
}
