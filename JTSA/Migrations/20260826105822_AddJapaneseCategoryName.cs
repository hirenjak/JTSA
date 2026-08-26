using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class AddJapaneseCategoryName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JapaneseDisplayName",
                table: "M_Category",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // 既存カテゴリもプレースホルダーで空にならないよう、現在名を初期値にする。
            migrationBuilder.Sql(
                "UPDATE M_Category SET JapaneseDisplayName = DisplayName WHERE JapaneseDisplayName = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JapaneseDisplayName",
                table: "M_Category");
        }
    }
}
