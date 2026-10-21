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
            migrationBuilder.Sql(@"
        IF COL_LENGTH('dbo.DeliveryOrders', 'ReceivedBy') IS NULL
            ALTER TABLE [dbo].[DeliveryOrders]
            ADD [ReceivedBy] nvarchar(max) NULL;

        IF COL_LENGTH('dbo.DeliveryOrders', 'ReceivedAt') IS NULL
            ALTER TABLE [dbo].[DeliveryOrders]
            ADD [ReceivedAt] datetime2 NULL;

        IF COL_LENGTH('dbo.DeliveryOrders', 'ReceptionNote') IS NULL
            ALTER TABLE [dbo].[DeliveryOrders]
            ADD [ReceptionNote] nvarchar(max) NULL;

        IF COL_LENGTH('dbo.OrderItems', 'OriginalPackageTypeId') IS NULL
            ALTER TABLE [dbo].[OrderItems]
            ADD [OriginalPackageTypeId] int NULL;

        IF COL_LENGTH('dbo.OrderItems', 'ClassificationNote') IS NULL
            ALTER TABLE [dbo].[OrderItems]
            ADD [ClassificationNote] nvarchar(max) NULL;

        IF COL_LENGTH('dbo.OrderItems', 'ClassifiedAt') IS NULL
            ALTER TABLE [dbo].[OrderItems]
            ADD [ClassifiedAt] datetime2 NULL;

        IF COL_LENGTH('dbo.OrderItems', 'ClassifiedBy') IS NULL
            ALTER TABLE [dbo].[OrderItems]
            ADD [ClassifiedBy] nvarchar(max) NULL;
    ");
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
