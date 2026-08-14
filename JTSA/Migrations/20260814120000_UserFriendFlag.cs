using JTSA.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260814120000_UserFriendFlag")]
public partial class UserFriendFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 既存のM_Userは従来すべてフレンドとして扱われていたため、その状態を維持する。
        migrationBuilder.AddColumn<bool>(
            name: "IsFriend",
            table: "M_User",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsFriend",
            table: "M_User");
    }
}
