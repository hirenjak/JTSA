using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JTSA.Migrations
{
    /// <inheritdoc />
    public partial class table_name_change : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_M_FriendList",
                table: "M_FriendList");

            migrationBuilder.DropPrimaryKey(
                name: "PK_T_TitleTextList",
                table: "T_TitleTextList");

            migrationBuilder.DropPrimaryKey(
                name: "PK_M_TitleTagList",
                table: "M_TitleTagList");

            migrationBuilder.DropPrimaryKey(
                name: "PK_M_SettingList",
                table: "M_SettingList");

            migrationBuilder.DropPrimaryKey(
                name: "PK_M_CategoryList",
                table: "M_CategoryList");

            migrationBuilder.RenameTable(
                name: "M_FriendList",
                newName: "M_User");

            migrationBuilder.RenameTable(
                name: "T_TitleTextList",
                newName: "T_TitleText");

            migrationBuilder.RenameTable(
                name: "M_TitleTagList",
                newName: "M_TitleTag");

            migrationBuilder.RenameTable(
                name: "M_SettingList",
                newName: "M_Setting");

            migrationBuilder.RenameTable(
                name: "M_CategoryList",
                newName: "M_Category");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_User",
                table: "M_User",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_T_TitleText",
                table: "T_TitleText",
                column: "BroadcastId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_TitleTag",
                table: "M_TitleTag",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_Setting",
                table: "M_Setting",
                column: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_Category",
                table: "M_Category",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_M_User",
                table: "M_User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_T_TitleText",
                table: "T_TitleText");

            migrationBuilder.DropPrimaryKey(
                name: "PK_M_TitleTag",
                table: "M_TitleTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_M_Setting",
                table: "M_Setting");

            migrationBuilder.DropPrimaryKey(
                name: "PK_M_Category",
                table: "M_Category");

            migrationBuilder.RenameTable(
                name: "M_User",
                newName: "M_FriendList");

            migrationBuilder.RenameTable(
                name: "T_TitleText",
                newName: "T_TitleTextList");

            migrationBuilder.RenameTable(
                name: "M_TitleTag",
                newName: "M_TitleTagList");

            migrationBuilder.RenameTable(
                name: "M_Setting",
                newName: "M_SettingList");

            migrationBuilder.RenameTable(
                name: "M_Category",
                newName: "M_CategoryList");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_FriendList",
                table: "M_FriendList",
                column: "BroadcastId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_T_TitleTextList",
                table: "T_TitleTextList",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_TitleTagList",
                table: "M_TitleTagList",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_SettingList",
                table: "M_SettingList",
                column: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_M_CategoryList",
                table: "M_CategoryList",
                column: "CategoryId");
        }
    }
}
