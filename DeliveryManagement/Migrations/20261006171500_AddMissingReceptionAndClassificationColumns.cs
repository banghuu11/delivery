using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingReceptionAndClassificationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "DeliveryOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "DeliveryOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionNote",
                table: "DeliveryOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalPackageTypeId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassificationNote",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClassifiedAt",
                table: "OrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassifiedBy",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "ReceptionNote",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "OriginalPackageTypeId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ClassificationNote",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ClassifiedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ClassifiedBy",
                table: "OrderItems");
        }
    }
}
