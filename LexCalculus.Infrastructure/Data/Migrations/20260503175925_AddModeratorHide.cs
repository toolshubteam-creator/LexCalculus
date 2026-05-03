using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModeratorHide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsModeratorHidden",
                table: "UserPosts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsModeratorHidden",
                table: "PostComments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserPosts_IsModeratorHidden",
                table: "UserPosts",
                column: "IsModeratorHidden");

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_IsModeratorHidden",
                table: "PostComments",
                column: "IsModeratorHidden");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPosts_IsModeratorHidden",
                table: "UserPosts");

            migrationBuilder.DropIndex(
                name: "IX_PostComments_IsModeratorHidden",
                table: "PostComments");

            migrationBuilder.DropColumn(
                name: "IsModeratorHidden",
                table: "UserPosts");

            migrationBuilder.DropColumn(
                name: "IsModeratorHidden",
                table: "PostComments");
        }
    }
}
