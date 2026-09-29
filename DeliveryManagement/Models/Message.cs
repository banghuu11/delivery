using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        // Cuộc trò chuyện
        public int ConversationId { get; set; }

        // Người gửi tin nhắn
        public string SenderId { get; set; } = string.Empty;

        // Nội dung
        public string Content { get; set; } = string.Empty;

        // Thời gian gửi
        public DateTime SentAt { get; set; } = DateTime.Now;

        // Navigation properties
        public Conversation? Conversation { get; set; }

        public ApplicationUser? Sender { get; set; }
    }
}