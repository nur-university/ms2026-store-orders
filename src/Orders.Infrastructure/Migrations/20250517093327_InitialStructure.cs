using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "outbox");

            migrationBuilder.CreateTable(
                name: "order",
                columns: table => new
                {
                    orderId = table.Column<Guid>(type: "uuid", nullable: false),
                    startedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    totalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    reservedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    procesingPaymentOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    canceledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order", x => x.orderId);
                });

            migrationBuilder.CreateTable(
                name: "outboxMessage",
                schema: "outbox",
                columns: table => new
                {
                    outboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                    processedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outboxMessage", x => x.outboxId);
                });

            migrationBuilder.CreateTable(
                name: "product",
                columns: table => new
                {
                    productId = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.productId);
                });

            migrationBuilder.CreateTable(
                name: "orderItem",
                columns: table => new
                {
                    orderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    orderId = table.Column<Guid>(type: "uuid", nullable: false),
                    productId = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orderItem", x => x.orderItemId);
                    table.ForeignKey(
                        name: "FK_orderItem_order_orderId",
                        column: x => x.orderId,
                        principalTable: "order",
                        principalColumn: "orderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_orderItem_product_productId",
                        column: x => x.productId,
                        principalTable: "product",
                        principalColumn: "productId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orderItem_orderId",
                table: "orderItem",
                column: "orderId");

            migrationBuilder.CreateIndex(
                name: "IX_orderItem_productId",
                table: "orderItem",
                column: "productId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orderItem");

            migrationBuilder.DropTable(
                name: "outboxMessage",
                schema: "outbox");

            migrationBuilder.DropTable(
                name: "order");

            migrationBuilder.DropTable(
                name: "product");
        }
    }
}
