using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class expansion_db_add : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_T_StreamExpansionItem_HeaderId",
                table: "T_StreamExpansionItem");

            migrationBuilder.CreateIndex(
                name: "IX_T_StreamExpansionItem_Id_HeaderId",
                table: "T_StreamExpansionItem",
                columns: new[] { "Id", "HeaderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_T_StreamExpansionItem_Id_HeaderId",
                table: "T_StreamExpansionItem");

            migrationBuilder.CreateIndex(
                name: "IX_T_StreamExpansionItem_HeaderId",
                table: "T_StreamExpansionItem",
                column: "HeaderId");
        }
    }
}
