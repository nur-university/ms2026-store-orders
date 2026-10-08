using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderUserAndProductPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "price",
                table: "product",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "userId",
                table: "order",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "price",
                table: "product");

            migrationBuilder.DropColumn(
                name: "userId",
                table: "order");
        }
    }
}
