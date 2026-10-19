using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        // Đơn hàng được đánh giá
        public string OrderCode { get; set; } = string.Empty;

        // Khách hàng đánh giá
        public string CustomerId { get; set; } = string.Empty;

        // Số sao đánh giá
        public int Rating { get; set; }

        // Nội dung đánh giá
        public string? Comment { get; set; }

        // Phản hồi của nhân viên
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