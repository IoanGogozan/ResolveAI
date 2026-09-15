using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveAI.CaseManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusinessReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                schema: "case_management",
                table: "cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                schema: "case_management",
                table: "cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentId",
                schema: "case_management",
                table: "cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_case_references",
                schema: "case_management",
                table: "cases",
                sql: "(\"CustomerId\" IS NULL AND \"OrderId\" IS NULL AND \"PaymentId\" IS NULL) OR (\"CustomerId\" IS NOT NULL AND \"OrderId\" IS NOT NULL AND \"PaymentId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_case_references",
                schema: "case_management",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "case_management",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "case_management",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                schema: "case_management",
                table: "cases");
        }
    }
}
