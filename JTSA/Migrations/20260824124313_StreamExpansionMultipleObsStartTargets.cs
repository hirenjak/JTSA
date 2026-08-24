using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class StreamExpansionMultipleObsStartTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsObsStreamStartMain",
                table: "T_StreamExpansionHeader",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // 単一選択だった既存設定では、Subが未選択ならMainが選択されていた。
            migrationBuilder.Sql("""
                UPDATE T_StreamExpansionHeader
                SET IsObsStreamStartMain = 1
                WHERE IsObsStreamStart = 1 AND IsObsStreamStartSub = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsObsStreamStartMain",
                table: "T_StreamExpansionHeader");
        }
    }
}
