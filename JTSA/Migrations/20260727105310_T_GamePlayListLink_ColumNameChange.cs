using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class T_GamePlayListLink_ColumNameChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastUpdatedDateTime",
                table: "T_GamePlayListLink",
                newName: "UpdatedDateTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedDateTime",
                table: "T_GamePlayListLink",
                newName: "LastUpdatedDateTime");
        }
    }
}
