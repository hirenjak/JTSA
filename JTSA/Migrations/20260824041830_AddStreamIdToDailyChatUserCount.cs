using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamIdToDailyChatUserCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_T_DailyChatUserCount",
                table: "T_DailyChatUserCount");

            migrationBuilder.AddColumn<string>(
                name: "StreamId",
                table: "T_DailyChatUserCount",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_T_DailyChatUserCount",
                table: "T_DailyChatUserCount",
                columns: new[] { "ChatDate", "UserId", "StreamId" });

            migrationBuilder.CreateIndex(
                name: "IX_T_DailyChatUserCount_StreamId",
                table: "T_DailyChatUserCount",
                column: "StreamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_T_DailyChatUserCount",
                table: "T_DailyChatUserCount");

            migrationBuilder.DropIndex(
                name: "IX_T_DailyChatUserCount_StreamId",
                table: "T_DailyChatUserCount");

            migrationBuilder.DropColumn(
                name: "StreamId",
                table: "T_DailyChatUserCount");

            migrationBuilder.AddPrimaryKey(
                name: "PK_T_DailyChatUserCount",
                table: "T_DailyChatUserCount",
                columns: new[] { "ChatDate", "UserId" });
        }
    }
}
