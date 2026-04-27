using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_Adim22_CalculationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalculationHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CategorySlug = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToolSlug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToolTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UserLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalculationHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalculationHistories_UserId",
                table: "CalculationHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalculationHistories_UserId_CreatedAt",
                table: "CalculationHistories",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalculationHistories_UserId_ToolSlug",
                table: "CalculationHistories",
                columns: new[] { "UserId", "ToolSlug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalculationHistories");
        }
    }
}
