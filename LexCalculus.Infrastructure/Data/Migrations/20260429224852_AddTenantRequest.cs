using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexCalculus.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ProposedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposedSlug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BarSicilNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<int>(type: "int", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedTenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantRequests_Tenants_CreatedTenantId",
                        column: x => x.CreatedTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_CreatedAt",
                table: "TenantRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_CreatedTenantId",
                table: "TenantRequests",
                column: "CreatedTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_ProcessedByUserId",
                table: "TenantRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_RequestedByUserId",
                table: "TenantRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_Status",
                table: "TenantRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantRequests");
        }
    }
}
