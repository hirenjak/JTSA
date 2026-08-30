using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class CalendarEntryTitleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Memo",
                table: "T_CalendarEntry",
                newName: "Content");

            migrationBuilder.AddColumn<string>(
                name: "CategoryBoxArtUrl",
                table: "T_CalendarEntry",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CategoryId",
                table: "T_CalendarEntry",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "T_CalendarEntry",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TitlePlaceholder",
                table: "T_CalendarEntry",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SelectedCount",
                table: "T_CalendarEntry",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortNumber",
                table: "T_CalendarEntry",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryBoxArtUrl",
                table: "T_CalendarEntry");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "T_CalendarEntry");

            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "T_CalendarEntry");

            migrationBuilder.DropColumn(
                name: "TitlePlaceholder",
                table: "T_CalendarEntry");

            migrationBuilder.DropColumn(
                name: "SelectedCount",
                table: "T_CalendarEntry");

            migrationBuilder.DropColumn(
                name: "SortNumber",
                table: "T_CalendarEntry");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "T_CalendarEntry",
                newName: "Memo");
        }
    }
}
