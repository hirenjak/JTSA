using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class StreamExpansionObsTextActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSubObs",
                table: "T_StreamExpansionItem",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ObsSceneName",
                table: "T_StreamExpansionItem",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObsSourceName",
                table: "T_StreamExpansionItem",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsObsStreamStart",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsObsStreamStartSub",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSubObs",
                table: "T_StreamExpansionItem");

            migrationBuilder.DropColumn(
                name: "ObsSceneName",
                table: "T_StreamExpansionItem");

            migrationBuilder.DropColumn(
                name: "ObsSourceName",
                table: "T_StreamExpansionItem");

            migrationBuilder.DropColumn(
                name: "IsObsStreamStart",
                table: "T_StreamExpansionHeader");

            migrationBuilder.DropColumn(
                name: "IsObsStreamStartSub",
                table: "T_StreamExpansionHeader");
        }
    }
}
