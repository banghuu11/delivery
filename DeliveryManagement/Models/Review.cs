
using System;
using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        // Đơn hàng được đánh giá
        [Required]
        public string OrderCode { get; set; } = string.Empty;

        // Khách hàng đánh giá
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        // Số sao: từ 1 đến 5
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        // Nội dung đánh giá
        [Required(ErrorMessage = "Vui lòng nhập nhận xét.")]
        [StringLength(1000, ErrorMessage = "Nhận xét không được vượt quá 1000 ký tự.")]
        public string Comment { get; set; } = string.Empty;

        // Thời gian khách hàng đánh giá
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Phản hồi của nhân viên
        [StringLength(1000)]
        public string? Response { get; set; }

        // Nhân viên phản hồi
        public string? RespondedBy { get; set; }

        // Thời gian phản hồi
        public DateTime? RespondedAt { get; set; }

        // Navigation properties
        public DeliveryOrder? DeliveryOrder { get; set; }

        public ApplicationUser? Customer { get; set; }

        public ApplicationUser? Staff { get; set; }
    }
}
