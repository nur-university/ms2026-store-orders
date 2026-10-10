using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftsAndAnnulment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "productName",
                table: "orderItem",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "cancellationReason",
                table: "order",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "refundRequired",
                table: "order",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedOn",
                table: "order",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing orders were last touched when they started.
            migrationBuilder.Sql("UPDATE \"order\" SET \"updatedOn\" = \"startedOn\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "productName",
                table: "orderItem");

            migrationBuilder.DropColumn(
                name: "cancellationReason",
                table: "order");

            migrationBuilder.DropColumn(
                name: "refundRequired",
                table: "order");

            migrationBuilder.DropColumn(
                name: "updatedOn",
                table: "order");
        }
    }
}
