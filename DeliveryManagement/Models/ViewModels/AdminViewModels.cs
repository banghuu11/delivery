using DeliveryManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingReceptionOrders { get; set; }
        public int InWarehouseOrders { get; set; }
        public int DeliveringOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public int TotalUsers { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalStaff { get; set; }

        public List<DeliveryOrder> RecentOrders { get; set; } = new();
        public List<PackageType> PackageTypes { get; set; } = new();
    }

    public class UserListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsLockedOut { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public string Role { get; set; } = string.Empty;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public string Role { get; set; } = string.Empty;

        public string? NewPassword { get; set; }
    }

    public class AdminCreateOrderViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn hoặc nhập khách hàng")]
        public string CustomerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên người gửi")]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập SĐT người gửi")]
        [Phone(ErrorMessage = "SĐT người gửi không hợp lệ")]
        public string SenderPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ người gửi")]
        public string SenderAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên người nhận")]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập SĐT người nhận")]
        [Phone(ErrorMessage = "SĐT người nhận không hợp lệ")]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ người nhận")]
        public string ReceiverAddress { get; set; } = string.Empty;

        public string DeliveryMethod { get; set; } = "Giao tiêu chuẩn";

        [Range(0.1, 10000, ErrorMessage = "Khoảng cách phải lớn hơn 0")]
        public decimal DistanceKm { get; set; } = 5;

        public string PaymentMethod { get; set; } = "Tiền mặt khi nhận hàng (COD)";
        public string PaymentStatus { get; set; } = "Chưa thanh toán";

        [Range(0, 1000000000, ErrorMessage = "Tổng tiền phải từ 0 trở lên")]
        public decimal TotalAmount { get; set; }

        public string CurrentStatus { get; set; } = "Chờ tiếp nhận";
        public string? DeliveryStaffId { get; set; }
        public string? DeliveryNote { get; set; }

        // Kiện hàng
        [Required(ErrorMessage = "Vui lòng chọn loại hàng hóa")]
        public int PackageTypeId { get; set; }

        [Range(0.01, 1000, ErrorMessage = "Khối lượng phải lớn hơn 0")]
        public decimal Weight { get; set; } = 1.0m;

        [Range(1, 1000, ErrorMessage = "Số lượng phải từ 1")]
        public int Quantity { get; set; } = 1;

        public string? Description { get; set; }
        public bool IsFragile { get; set; }
        public bool IsValuable { get; set; }
    }

    public class AdminEditOrderViewModel
    {
        [Required]
        public string OrderCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn khách hàng")]
        public string CustomerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên người gửi")]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập SĐT người gửi")]
        public string SenderPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ người gửi")]
        public string SenderAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên người nhận")]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập SĐT người nhận")]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ người nhận")]
        public string ReceiverAddress { get; set; } = string.Empty;

        public string DeliveryMethod { get; set; } = "Giao tiêu chuẩn";
        public decimal DistanceKm { get; set; }

        public string PaymentMethod { get; set; } = "Tiền mặt khi nhận hàng (COD)";
        public string PaymentStatus { get; set; } = "Chưa thanh toán";
        public decimal TotalAmount { get; set; }

        public string CurrentStatus { get; set; } = "Chờ tiếp nhận";
        public string? DeliveryStaffId { get; set; }
        public string? DeliveryNote { get; set; }
    }
}

