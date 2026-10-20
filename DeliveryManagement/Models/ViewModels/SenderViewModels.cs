using System.ComponentModel.DataAnnotations;
using DeliveryManagement.Models;

namespace DeliveryManagement.Models.ViewModels
{
    // ==========================================
    // 1. VIEW MODEL HỒ SƠ NGƯỜI GỬI / CÁ NHÂN
    // ==========================================
    public class SenderProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại liên hệ")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ lấy hàng mặc định")]
        public string? DefaultSenderAddress { get; set; }

        // Danh sách địa chỉ gửi hàng đã lưu.
        // Chỉ Customer được sử dụng theo phân quyền của ứng dụng.
        public List<SenderAddress> SavedAddresses { get; set; } = new();

        // Thống kê đơn gửi cá nhân.
        public int TotalOrdersSent { get; set; }

        public int PendingOrders { get; set; }

        public int DeliveringOrders { get; set; }

        public int CompletedOrders { get; set; }

        public decimal TotalSpent { get; set; }
    }

    // ==========================================
    // 2. MODEL THÊM / SỬA ĐỊA CHỈ NGƯỜI GỬI
    // ==========================================
    public class SenderAddressInputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên người gửi")]
        [Display(Name = "Họ tên người gửi")]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string SenderPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ lấy hàng")]
        [Display(Name = "Địa chỉ chi tiết")]
        public string SenderAddressText { get; set; } = string.Empty;

        [Display(Name = "Nhãn địa chỉ")]
        public string? AddressTag { get; set; }

        [Display(Name = "Đặt làm địa chỉ mặc định")]
        public bool IsDefault { get; set; }
    }

    // ==========================================
    // 3. MODEL ĐỔI MẬT KHẨU
    // ==========================================
    public class ChangePasswordInputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage = "Mật khẩu mới phải từ 6 đến 100 ký tự")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [Compare(
            nameof(NewPassword),
            ErrorMessage = "Mật khẩu xác nhận không khớp với mật khẩu mới")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}