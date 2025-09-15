using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class _20250915 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "M_StreamWindowList",
                columns: table => new
                {
                    ProcessName = table.Column<string>(type: "TEXT", nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", nullable: false),
                    X = table.Column<int>(type: "INTEGER", nullable: false),
                    Y = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUseDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M_StreamWindowList", x => x.ProcessName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "M_StreamWindowList");
        }
    }
}
