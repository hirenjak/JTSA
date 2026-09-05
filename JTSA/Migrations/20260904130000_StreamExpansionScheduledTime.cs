using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260904130000_StreamExpansionScheduledTime")]
public partial class StreamExpansionScheduledTime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsScheduledTime", table: "T_StreamExpansionHeader",
            type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "ScheduledHour", table: "T_StreamExpansionHeader",
            type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "ScheduledMinute", table: "T_StreamExpansionHeader",
            type: "INTEGER", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsScheduledTime", table: "T_StreamExpansionHeader");
        migrationBuilder.DropColumn(name: "ScheduledHour", table: "T_StreamExpansionHeader");
        migrationBuilder.DropColumn(name: "ScheduledMinute", table: "T_StreamExpansionHeader");
    }
}
