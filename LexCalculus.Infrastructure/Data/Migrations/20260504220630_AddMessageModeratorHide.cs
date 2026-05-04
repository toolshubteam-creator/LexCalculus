using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageModeratorHide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsModeratorHidden",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsModeratorHidden",
                table: "Messages",
                column: "IsModeratorHidden");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_IsModeratorHidden",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsModeratorHidden",
                table: "Messages");
        }
    }
}
