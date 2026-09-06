using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260906170000_UserStreamingProfile")]
public partial class UserStreamingProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StreamingPlatform",
            table: "M_User",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "StreamingUrl",
            table: "M_User",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "StreamingPlatform", table: "M_User");
        migrationBuilder.DropColumn(name: "StreamingUrl", table: "M_User");
    }
}
