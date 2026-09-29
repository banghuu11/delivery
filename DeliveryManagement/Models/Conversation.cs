using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class Conversation
    {
        [Key]
        public int ConversationId { get; set; }

        // Khách hàng
        public string CustomerId { get; set; } = string.Empty;

        // Nhân viên hỗ trợ
        public string? StaffId { get; set; }

        // Trạng thái cuộc trò chuyện
        public string Status { get; set; } = "Open";

        // Thời gian tạo
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ApplicationUser? Customer { get; set; }

        public ApplicationUser? Staff { get; set; }

        public ICollection<Message> Messages { get; set; }
            = new List<Message>();
    }
}