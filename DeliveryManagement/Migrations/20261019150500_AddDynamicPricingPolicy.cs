using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicPricingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingPolicies",
                columns: table => new
                {
                    PricingPolicyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseDistanceKm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    StandardBasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StandardPricePerKm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpressBasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpressPricePerKm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WeightThresholdKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ExtraPricePerKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WeatherRainSurcharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WeatherStormSurcharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TrafficJamSurcharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EnableWeatherSurcharge = table.Column<bool>(type: "bit", nullable: false),
                    EnableTrafficSurcharge = table.Column<bool>(type: "bit", nullable: false),
                    TomTomApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPolicies", x => x.PricingPolicyId);
                });

            migrationBuilder.AddColumn<decimal>(
                name: "BaseShippingFee",
                table: "DeliveryOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightFee",
                table: "DeliveryOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WeatherFee",
                table: "DeliveryOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TrafficFee",
                table: "DeliveryOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WeatherDescription",
                table: "DeliveryOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrafficDescription",
                table: "DeliveryOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingPolicies");

            migrationBuilder.DropColumn(
                name: "BaseShippingFee",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "WeightFee",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "WeatherFee",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "TrafficFee",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "WeatherDescription",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "TrafficDescription",
                table: "DeliveryOrders");
        }
    }
}
