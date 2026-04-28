using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormulaParameterAuditUserIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "FormulaParameters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastModifiedByUserId",
                table: "FormulaParameters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormulaParameters_CreatedByUserId",
                table: "FormulaParameters",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormulaParameters_LastModifiedByUserId",
                table: "FormulaParameters",
                column: "LastModifiedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormulaParameters_AspNetUsers_CreatedByUserId",
                table: "FormulaParameters",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FormulaParameters_AspNetUsers_LastModifiedByUserId",
                table: "FormulaParameters",
                column: "LastModifiedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormulaParameters_AspNetUsers_CreatedByUserId",
                table: "FormulaParameters");

            migrationBuilder.DropForeignKey(
                name: "FK_FormulaParameters_AspNetUsers_LastModifiedByUserId",
                table: "FormulaParameters");

            migrationBuilder.DropIndex(
                name: "IX_FormulaParameters_CreatedByUserId",
                table: "FormulaParameters");

            migrationBuilder.DropIndex(
                name: "IX_FormulaParameters_LastModifiedByUserId",
                table: "FormulaParameters");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "FormulaParameters");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserId",
                table: "FormulaParameters");
        }
    }
}
