using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class OrderStatusHistory
    {
        [Key]
        public int HistoryId { get; set; }

        // Đơn hàng
        public string OrderCode { get; set; } = string.Empty;

        // Trạng thái
        public string Status { get; set; } = string.Empty;

        // Người cập nhật trạng thái
        public string? ChangedBy { get; set; }

        // Thời gian cập nhật
        public DateTime ChangedAt { get; set; } = DateTime.Now;

        // Navigation property
        public DeliveryOrder? DeliveryOrder { get; set; }
    }
}