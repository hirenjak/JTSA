using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class CalendarMultipleEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_T_CalendarEntry_CalendarDate",
                table: "T_CalendarEntry");

            migrationBuilder.CreateIndex(
                name: "IX_T_CalendarEntry_CalendarDate_StartTime",
                table: "T_CalendarEntry",
                columns: new[] { "CalendarDate", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_T_CalendarEntry_CalendarDate_StartTime",
                table: "T_CalendarEntry");

            migrationBuilder.CreateIndex(
                name: "IX_T_CalendarEntry_CalendarDate",
                table: "T_CalendarEntry",
                column: "CalendarDate",
                unique: true);
        }
    }
}
