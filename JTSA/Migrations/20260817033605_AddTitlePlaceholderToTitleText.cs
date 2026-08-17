using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class AddTitlePlaceholderToTitleText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TitlePlaceholder",
                table: "T_TitleText",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TitlePlaceholder",
                table: "T_TitleText");
        }
    }
}
