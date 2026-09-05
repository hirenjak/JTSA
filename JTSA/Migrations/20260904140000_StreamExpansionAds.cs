using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260904140000_StreamExpansionAds")]
public partial class StreamExpansionAds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "IsAdStart", "IsAdEnd", "IsAdUpcoming" })
            migrationBuilder.AddColumn<bool>(name: name, table: "T_StreamExpansionHeader", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "AdAdvanceMinutes", table: "T_StreamExpansionHeader", type: "INTEGER", nullable: false, defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "IsAdStart", "IsAdEnd", "IsAdUpcoming", "AdAdvanceMinutes" })
            migrationBuilder.DropColumn(name: name, table: "T_StreamExpansionHeader");
    }
}
