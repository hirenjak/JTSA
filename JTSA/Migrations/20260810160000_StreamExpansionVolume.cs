using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260810160000_StreamExpansionVolume")]
public partial class StreamExpansionVolume : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Volume",
            table: "T_StreamExpansionItem",
            type: "INTEGER",
            nullable: false,
            defaultValue: 100);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Volume",
            table: "T_StreamExpansionItem");
    }
}
