using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260910120000_AppNotificationReceipts")]
public sealed class AppNotificationReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.CreateTable(
            name: "T_AppNotificationReceipt",
            columns: table => new
            {
                NotificationKey = table.Column<string>(type: "TEXT", nullable: false),
                AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_T_AppNotificationReceipt", x => x.NotificationKey);
            });

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "T_AppNotificationReceipt");
}
