using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class StreamExpansionChatPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChatPermissionEveryone",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChatPermissionModerator",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChatPermissionSubscriber",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChatPermissionVip",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatPermissionEveryone",
                table: "T_StreamExpansionHeader");

            migrationBuilder.DropColumn(
                name: "ChatPermissionModerator",
                table: "T_StreamExpansionHeader");

            migrationBuilder.DropColumn(
                name: "ChatPermissionSubscriber",
                table: "T_StreamExpansionHeader");

            migrationBuilder.DropColumn(
                name: "ChatPermissionVip",
                table: "T_StreamExpansionHeader");
        }
    }
}
