using DeliveryManagement.Models;

namespace DeliveryManagement.Services
{
    public class ChatMessageDto
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public bool IsFromAi { get; set; }
        public bool IsFromStaff { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string FormattedTime => SentAt.ToString("HH:mm - dd/MM");
    }

    public class OrderSummaryDto
    {
        public string OrderCode { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public string ReceiverPhone { get; set; } = string.Empty;
        public string ReceiverAddress { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = "bg-secondary";
        public decimal TotalAmount { get; set; }
        public string DeliveryMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ItemsCount { get; set; }
        public string? LatestTrackingNote { get; set; }
    }

    public class ChatFeeCalculationDto
    {
        public string DeliveryMethod { get; set; } = "Thường";
        public decimal WeightKg { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal BaseShippingFee { get; set; }
        public decimal WeightFee { get; set; }
        public decimal WeatherFee { get; set; }
        public decimal TrafficFee { get; set; }
        public decimal TotalPrice { get; set; }
        public string WeatherDescription { get; set; } = "Bình thường";
        public string TrafficDescription { get; set; } = "Bình thường";
    }

    public class ChatResponseDto
    {
        public bool Success { get; set; } = true;
        public int ConversationId { get; set; }
        public string Reply { get; set; } = string.Empty;
        public List<string> QuickPrompts { get; set; } = new();
        public List<OrderSummaryDto>? Orders { get; set; }
        public ChatFeeCalculationDto? EstimatedFee { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
    }

    public interface IAiChatService
    {
        Task<ChatResponseDto> ProcessMessageAsync(string userMessage, string? userId, int? conversationId = null);
        Task<List<ChatMessageDto>> GetConversationHistoryAsync(int conversationId, string? currentUserId);
        Task<Conversation?> GetOrCreateConversationAsync(string? userId, int? conversationId = null);
        Task<ChatFeeCalculationDto?> EstimateFeeDirectAsync(string deliveryMethod, decimal weight, decimal distance);
    }
}
