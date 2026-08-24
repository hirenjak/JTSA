using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class AggregateChatCountsByStream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "T_StreamChatUserCount",
                columns: table => new
                {
                    StreamId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    ChatCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstChatDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastChatDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_StreamChatUserCount", x => new { x.StreamId, x.UserId });
                });

            migrationBuilder.Sql("""
                INSERT INTO "T_StreamChatUserCount"
                    ("StreamId", "UserId", "LoginId", "DisplayName", "ChatCount",
                     "FirstChatDateTime", "LastChatDateTime", "CreatedDateTime", "UpdatedDateTime")
                SELECT
                    CASE
                        WHEN "StreamId" = '' THEN 'untracked-' || strftime('%Y%m%d', "ChatDate")
                        ELSE "StreamId"
                    END,
                    "UserId",
                    MAX("LoginId"),
                    MAX("DisplayName"),
                    SUM("ChatCount"),
                    MIN("ChatDate"),
                    MAX("ChatDate"),
                    MIN("CreatedDateTime"),
                    MAX("UpdatedDateTime")
                FROM "T_DailyChatUserCount"
                GROUP BY
                    CASE
                        WHEN "StreamId" = '' THEN 'untracked-' || strftime('%Y%m%d', "ChatDate")
                        ELSE "StreamId"
                    END,
                    "UserId";
                """);

            migrationBuilder.DropTable(
                name: "T_DailyChatUserCount");

            migrationBuilder.CreateIndex(
                name: "IX_T_StreamChatUserCount_StreamId",
                table: "T_StreamChatUserCount",
                column: "StreamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "T_StreamChatUserCount");

            migrationBuilder.CreateTable(
                name: "T_DailyChatUserCount",
                columns: table => new
                {
                    ChatDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    StreamId = table.Column<string>(type: "TEXT", nullable: false),
                    ChatCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    LoginId = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_DailyChatUserCount", x => new { x.ChatDate, x.UserId, x.StreamId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_T_DailyChatUserCount_StreamId",
                table: "T_DailyChatUserCount",
                column: "StreamId");
        }
    }
}
