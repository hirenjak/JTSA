using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260909120000_ChatUserHistoryIndex")]
public class ChatUserHistoryIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.CreateIndex(
            name: "IX_T_StreamChatUserCount_UserId_FirstChatDateTime",
            table: "T_StreamChatUserCount",
            columns: new[] { "UserId", "FirstChatDateTime" });

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropIndex(
            name: "IX_T_StreamChatUserCount_UserId_FirstChatDateTime",
            table: "T_StreamChatUserCount");
}
