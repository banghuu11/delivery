using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models.ViewModels
{
    public class WarehouseOrderViewModel
    {
        public string OrderCode { get; set; } = string.Empty;

        public string CurrentStatus { get; set; } = string.Empty;

        public string DeliveryMethod { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public List<WarehouseItemViewModel> Items { get; set; } = new();
    }

    public class WarehouseItemViewModel
    {
        public int OrderItemId { get; set; }

        // Loại khách khai báo ban đầu
        public int? OriginalPackageTypeId { get; set; }

        public string OriginalPackageTypeName { get; set; } = string.Empty;

        // Loại đang được phân loại
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại hàng hóa.")]
        public int PackageTypeId { get; set; }

        public string CurrentPackageTypeName { get; set; } = string.Empty;

        // Thông tin hàng
        public string? Description { get; set; }

        public string? Size { get; set; }

        public int Quantity { get; set; }

        public decimal Weight { get; set; }

        public bool IsFragile { get; set; }

        public bool IsValuable { get; set; }

        // Ghi chú phân loại
        [StringLength(500)]
        public string? ClassificationNote { get; set; }
    }
}