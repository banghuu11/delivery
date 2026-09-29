using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }

        // Đơn hàng
        public string OrderCode { get; set; } = string.Empty;

        // Loại hàng hóa / quy cách đóng gói
        public int PackageTypeId { get; set; }

        // Mô tả hàng hóa
        public string Description { get; set; } = string.Empty;

        // Kích thước
        public string? Size { get; set; }

        // Số lượng
        public int Quantity { get; set; }

        // Khối lượng (kg)
        public decimal Weight { get; set; }

        // Hàng dễ vỡ
        public bool IsFragile { get; set; }

        // Hàng có giá trị
        public bool IsValuable { get; set; }

        // Navigation properties
        public DeliveryOrder? DeliveryOrder { get; set; }

        public PackageType? PackageType { get; set; }
    }
}