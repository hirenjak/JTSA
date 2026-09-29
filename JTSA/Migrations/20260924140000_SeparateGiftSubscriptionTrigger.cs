using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260924140000_SeparateGiftSubscriptionTrigger")]
public partial class SeparateGiftSubscriptionTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsGiftSubscription",
            table: "T_StreamExpansionHeader",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
        migrationBuilder.Sql("UPDATE T_StreamExpansionHeader SET IsGiftSubscription = IsSubscribe");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsGiftSubscription", table: "T_StreamExpansionHeader");
    }
}
