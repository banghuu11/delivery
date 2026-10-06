using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class DeliveryOrder
    {
        [Key]
        public string OrderCode { get; set; } = string.Empty;

        // Khách hàng
        public string CustomerId { get; set; } = string.Empty;

        // Thông tin người gửi
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string SenderAddress { get; set; } = string.Empty;

        // Thông tin người nhận
        public string ReceiverName { get; set; } = string.Empty;
        public string ReceiverPhone { get; set; } = string.Empty;
        public string ReceiverAddress { get; set; } = string.Empty;

        // Thông tin giao hàng
        public string DeliveryMethod { get; set; } = string.Empty;
        [Precision(10, 2)]
        public decimal DistanceKm { get; set; }

        // Thông tin thanh toán
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal? PaymentAmount { get; set; }
        public DateTime? PaymentTime { get; set; }

        // Tiền đơn hàng
        public decimal TotalAmount { get; set; }

        // Trạng thái đơn hàng
        public string CurrentStatus { get; set; } = string.Empty;

        // Nhân viên giao hàng
        public string? DeliveryStaffId { get; set; }

        // Kết quả giao hàng
        public string? DeliveryResult { get; set; }
        public string? ProofImage { get; set; }
        public string? DeliveryNote { get; set; }

        // Thời gian tạo đơn
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ApplicationUser? Customer { get; set; }
        public ApplicationUser? DeliveryStaff { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();

        public ICollection<OrderStatusHistory> OrderStatusHistories { get; set; }
            = new List<OrderStatusHistory>();
        // Thông tin tiếp nhận
        public string? ReceivedBy { get; set; }

        public DateTime? ReceivedAt { get; set; }

        public string? ReceptionNote { get; set; }
    }
}