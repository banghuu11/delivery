using System;
using DeliveryManagement.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryManagement.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261104090000_AddShipperLocations")]
    public partial class AddShipperLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipperLocations",
                columns: table => new
                {
                    OrderCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeliveryStaffId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Accuracy = table.Column<double>(type: "float", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipperLocations", x => x.OrderCode);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipperLocations");
        }
    }
}
