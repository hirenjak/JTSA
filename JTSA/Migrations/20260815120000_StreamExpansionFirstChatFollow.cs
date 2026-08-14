using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815120000_StreamExpansionFirstChatFollow")]
public partial class StreamExpansionFirstChatFollow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFirstChat",
            table: "T_StreamExpansionHeader",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsFollow",
            table: "T_StreamExpansionHeader",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsFirstChat", table: "T_StreamExpansionHeader");
        migrationBuilder.DropColumn(name: "IsFollow", table: "T_StreamExpansionHeader");
    }
}
