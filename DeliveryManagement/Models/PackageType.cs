using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class PackageType
    {
        [Key]
        public int PackageTypeId { get; set; }

        // Tên loại hàng hóa
        public string TypeName { get; set; } = string.Empty;

        // Mô tả
        public string? Description { get; set; }

        // Đang sử dụng hay không
        public bool IsActive { get; set; } = true;

        // Navigation property
        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}