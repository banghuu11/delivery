using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class SenderAddress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên người gửi")]
        [Display(Name = "Họ và tên người gửi")]
        [StringLength(100)]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại người gửi")]
        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [StringLength(20)]
        public string SenderPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ lấy hàng")]
        [Display(Name = "Địa chỉ lấy hàng")]
        [StringLength(300)]
        public string SenderAddressText { get; set; } = string.Empty;

        [Display(Name = "Nhãn địa chỉ")]
        [StringLength(50)]
        public string? AddressTag { get; set; } // Ví dụ: "Kho hàng chính", "Nhà riêng", "Cửa hàng", "Văn phòng"

        [Display(Name = "Địa chỉ mặc định")]
        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public ApplicationUser? User { get; set; }
    }
}
