using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveAI.AI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "agent_runs",
                schema: "ai",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Provider = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProposalJson = table.Column<string>(type: "jsonb", nullable: true),
                    FactsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TraceParent = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuditDeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.RunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_TenantId_CaseId",
                schema: "ai",
                table: "agent_runs",
                columns: new[] { "TenantId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_TenantId_OperationId",
                schema: "ai",
                table: "agent_runs",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_runs",
                schema: "ai");
        }
    }
}
