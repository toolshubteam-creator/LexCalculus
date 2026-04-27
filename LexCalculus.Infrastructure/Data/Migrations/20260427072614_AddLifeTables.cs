using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLifeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LifeTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifeTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LifeTableRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LifeTableId = table.Column<int>(type: "int", nullable: false),
                    Yas = table.Column<int>(type: "int", nullable: false),
                    Cinsiyet = table.Column<int>(type: "int", nullable: false),
                    BekledigiYasam = table.Column<decimal>(type: "decimal(8,6)", precision: 8, scale: 6, nullable: false),
                    OlumOlasiligi = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifeTableRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LifeTableRows_LifeTables_LifeTableId",
                        column: x => x.LifeTableId,
                        principalTable: "LifeTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LifeTableRows_Lookup",
                table: "LifeTableRows",
                columns: new[] { "LifeTableId", "Yas", "Cinsiyet" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LifeTables_Code",
                table: "LifeTables",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LifeTableRows");

            migrationBuilder.DropTable(
                name: "LifeTables");
        }
    }
}
