using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTracingToOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CorrelationId",
                schema: "outbox",
                table: "outboxMessage",
                newName: "correlationId");

            migrationBuilder.AddColumn<string>(
                name: "spanId",
                schema: "outbox",
                table: "outboxMessage",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "traceId",
                schema: "outbox",
                table: "outboxMessage",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "spanId",
                schema: "outbox",
                table: "outboxMessage");

            migrationBuilder.DropColumn(
                name: "traceId",
                schema: "outbox",
                table: "outboxMessage");

            migrationBuilder.RenameColumn(
                name: "correlationId",
                schema: "outbox",
                table: "outboxMessage",
                newName: "CorrelationId");
        }
    }
}
