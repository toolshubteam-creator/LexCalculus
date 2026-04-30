using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "UserProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTenant",
                table: "UserProfiles",
                type: "bit",
                nullable: false,
                defaultValueSql: "0");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_PublicSlug",
                table: "UserProfiles",
                column: "PublicSlug",
                unique: true,
                filter: "[PublicSlug] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_PublicSlug",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ShowTenant",
                table: "UserProfiles");
        }
    }
}
