using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class user_add_colomn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_M_User",
                table: "M_User");

            migrationBuilder.RenameColumn(
                name: "BroadcastId",
                table: "M_User",
                newName: "LoginId");

            migrationBuilder.AddColumn<string>(
                name: "UserIconBitmap",
                table: "M_User",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_User",
                table: "M_User",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_M_User",
                table: "M_User");

            migrationBuilder.DropColumn(
                name: "UserIconBitmap",
                table: "M_User");

            migrationBuilder.RenameColumn(
                name: "LoginId",
                table: "M_User",
                newName: "BroadcastId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_User",
                table: "M_User",
                column: "BroadcastId");
        }
    }
}
