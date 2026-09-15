using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveAI.CaseManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "case_management");

            migrationBuilder.CreateTable(
                name: "cases",
                schema: "case_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cases", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "case_created_outbox",
                schema: "case_management",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TraceParent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_created_outbox", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_case_created_outbox_cases_TenantId_CaseId",
                        columns: x => new { x.TenantId, x.CaseId },
                        principalSchema: "case_management",
                        principalTable: "cases",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_created_outbox_OccurredAt",
                schema: "case_management",
                table: "case_created_outbox",
                column: "OccurredAt",
                filter: "\"DeliveredAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_case_created_outbox_TenantId_CaseId",
                schema: "case_management",
                table: "case_created_outbox",
                columns: new[] { "TenantId", "CaseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_created_outbox",
                schema: "case_management");

            migrationBuilder.DropTable(
                name: "cases",
                schema: "case_management");
        }
    }
}
