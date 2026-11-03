
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryManagement.Migrations
{
    public partial class AddReviewCreatedAtAndUniqueOrderCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Xóa index cũ để tạo lại index duy nhất theo OrderCode.
            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderCode",
                table: "Reviews");

            // Giới hạn nội dung phản hồi của nhân viên.
            migrationBuilder.AlterColumn<string>(
                name: "Response",
                table: "Reviews",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Bắt buộc khách hàng nhập nội dung đánh giá.
            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "Reviews",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Thời gian khách hàng gửi đánh giá.
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Reviews",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            // Mỗi đơn hàng chỉ được có một đánh giá.
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderCode",
                table: "Reviews",
                column: "OrderCode",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderCode",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Reviews");

            migrationBuilder.AlterColumn<string>(
                name: "Response",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderCode",
                table: "Reviews",
                column: "OrderCode");
        }
    }
}
