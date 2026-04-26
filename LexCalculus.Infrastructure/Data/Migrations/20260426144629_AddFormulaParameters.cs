using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormulaParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormulaParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ToolSlug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    IsAutoUpdated = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormulaParameters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormulaParameters_Lookup",
                table: "FormulaParameters",
                columns: new[] { "ToolSlug", "Key", "EffectiveDate" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "UX_FormulaParameters_Version",
                table: "FormulaParameters",
                columns: new[] { "ToolSlug", "Key", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormulaParameters");
        }
    }
}
