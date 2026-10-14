using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models.ViewModels
{
    public class ReceptionViewModel
    {
        public string OrderCode { get; set; } = string.Empty;

        // =========================
        // THÔNG TIN NGƯỜI NHẬN
        // =========================

        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận.")]
        [StringLength(100)]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại người nhận.")]
        [RegularExpression(
            @"^0\d{9}$",
            ErrorMessage = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.")]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ người nhận.")]
        [StringLength(255)]
        public string ReceiverAddress { get; set; } = string.Empty;

        // =========================
        // GHI CHÚ TIẾP NHẬN
        // =========================

        [StringLength(500)]
        public string? ReceptionNote { get; set; }

        // =========================
        // HÀNG HÓA
        // =========================

        public List<ReceptionItemViewModel> Items { get; set; } = new();
    }

    public class ReceptionItemViewModel
    {
        public int OrderItemId { get; set; }

        public int PackageTypeId { get; set; }

        public string PackageTypeName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Size { get; set; }

        [Range(1, 1000, ErrorMessage = "Số lượng phải từ 1 đến 1000.")]
        public int Quantity { get; set; }

        [Range(0.01, 10000, ErrorMessage = "Khối lượng phải lớn hơn 0.")]
        public decimal Weight { get; set; }

        public bool IsFragile { get; set; }

        public bool IsValuable { get; set; }
    }
}