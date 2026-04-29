using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMeslekAndContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaroSicilNo",
                table: "AspNetUsers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeslekTuru",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeslekTuruDiger",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefon",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Faz 3.6: Premium ve Free rol setinden çıkarıldı.
            // Yerine Admin/Editor/Kullanici (idempotent seed IdentitySeeder'da).
            migrationBuilder.Sql(@"
                DELETE FROM AspNetUserRoles
                WHERE RoleId IN (SELECT Id FROM AspNetRoles WHERE Name IN ('Premium', 'Free'));

                DELETE FROM AspNetRoles WHERE Name IN ('Premium', 'Free');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaroSicilNo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MeslekTuru",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MeslekTuruDiger",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Telefon",
                table: "AspNetUsers");

            // ApplicationRole : IdentityRole<int> — Id IDENTITY column, otomatik atanır.
            // Rollback için Premium/Free rolleri yeniden eklenir,
            // ama atanmış kullanıcı geri yüklenemez (Up sırasında silindi).
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Premium')
                    INSERT INTO AspNetRoles (Name, NormalizedName, ConcurrencyStamp)
                    VALUES ('Premium', 'PREMIUM', CONVERT(nvarchar(36), NEWID()));

                IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Free')
                    INSERT INTO AspNetRoles (Name, NormalizedName, ConcurrencyStamp)
                    VALUES ('Free', 'FREE', CONVERT(nvarchar(36), NEWID()));
            ");
        }
    }
}
