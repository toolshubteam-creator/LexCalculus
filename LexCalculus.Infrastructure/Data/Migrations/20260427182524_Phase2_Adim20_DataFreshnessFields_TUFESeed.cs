using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_Adim20_DataFreshnessFields_TUFESeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedUpdateFrequency",
                table: "FormulaParameters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedDate",
                table: "FormulaParameters",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FormulaParameters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedUpdateFrequency",
                table: "FormulaParameters");

            migrationBuilder.DropColumn(
                name: "LastUpdatedDate",
                table: "FormulaParameters");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FormulaParameters");
        }
    }
}
