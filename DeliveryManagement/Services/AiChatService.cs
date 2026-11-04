using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DeliveryManagement.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPricingService _pricingService;
        private readonly ILogger<AiChatService> _logger;

        private const string AiEmail = "ai-assistant@delivery.com";
        private const string GuestEmail = "guest@delivery.com";

        public AiChatService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPricingService pricingService,
            ILogger<AiChatService> logger)
        {
            _context = context;
            _userManager = userManager;
            _pricingService = pricingService;
            _logger = logger;
        }

        private async Task<ApplicationUser> EnsureAiUserAsync()
        {
            var user = await _userManager.FindByEmailAsync(AiEmail);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = AiEmail,
                    Email = AiEmail,
                    EmailConfirmed = true,
                    FullName = "Trợ lý AI Tốc Độ"
                };
                await _userManager.CreateAsync(user, "AiBot@123");
                await _userManager.AddToRoleAsync(user, "ReceptionStaff");
            }
            return user;
        }

        private async Task<ApplicationUser> EnsureGuestUserAsync()
        {
            var user = await _userManager.FindByEmailAsync(GuestEmail);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = GuestEmail,
                    Email = GuestEmail,
                    EmailConfirmed = true,
                    FullName = "Khách vãng lai"
                };
                await _userManager.CreateAsync(user, "Guest@123");
                await _userManager.AddToRoleAsync(user, "Customer");
            }
            return user;
        }

        public async Task<Conversation?> GetOrCreateConversationAsync(string? userId, int? conversationId = null)
        {
            if (conversationId.HasValue && conversationId.Value > 0)
            {
                var existing = await _context.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.ConversationId == conversationId.Value);

                if (existing != null)
                {
                    return existing;
                }
            }

            string actualUserId = userId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actualUserId))
            {
                var guest = await EnsureGuestUserAsync();
                actualUserId = guest.Id;
            }

            // Tìm cuộc hội thoại còn Open gần nhất
            var conv = await _context.Conversations
                .Include(c => c.Messages)
                .Where(c => c.CustomerId == actualUserId && c.Status == "Open")
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (conv == null)
            {
                var aiUser = await EnsureAiUserAsync();
                conv = new Conversation
                {
                    CustomerId = actualUserId,
                    StaffId = aiUser.Id,
                    Status = "Open",
                    CreatedAt = DateTime.Now
                };
                _context.Conversations.Add(conv);
                await _context.SaveChangesAsync();
            }

            return conv;
        }

        public async Task<List<ChatMessageDto>> GetConversationHistoryAsync(int conversationId, string? currentUserId)
        {
            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var aiUser = await EnsureAiUserAsync();

            return messages.Select(m => new ChatMessageDto
            {
                MessageId = m.MessageId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.FullName ?? (m.SenderId == aiUser.Id ? "Trợ lý AI Tốc Độ" : "Khách hàng"),
                IsFromAi = m.SenderId == aiUser.Id || m.Sender?.Email == AiEmail,
                IsFromStaff = m.Sender?.Email != AiEmail && m.SenderId != currentUserId,
                Content = m.Content,
                SentAt = m.SentAt
            }).ToList();
        }

        public async Task<ChatFeeCalculationDto?> EstimateFeeDirectAsync(string deliveryMethod, decimal weight, decimal distance)
        {
            if (weight <= 0) weight = 1;
            if (distance <= 0) distance = 5;
            string method = deliveryMethod.Contains("nhanh", StringComparison.OrdinalIgnoreCase) || deliveryMethod.Contains("hỏa", StringComparison.OrdinalIgnoreCase)
                ? "Nhanh" : "Thường";

            var result = await _pricingService.CalculatePriceAsync(method, weight, distance);
            if (!result.Success)
            {
                return null;
            }

            return new ChatFeeCalculationDto
            {
                DeliveryMethod = method,
                WeightKg = weight,
                DistanceKm = distance,
                BaseShippingFee = result.BaseShippingFee,
                WeightFee = result.WeightFee,
                WeatherFee = result.WeatherFee,
                TrafficFee = result.TrafficFee,
                TotalPrice = result.TotalPrice,
                WeatherDescription = result.WeatherDescription,
                TrafficDescription = result.TrafficDescription
            };
        }

        public async Task<ChatResponseDto> ProcessMessageAsync(string userMessage, string? userId, int? conversationId = null)
        {
            var conv = await GetOrCreateConversationAsync(userId, conversationId);
            if (conv == null)
            {
                return new ChatResponseDto
                {
                    Success = false,
                    Reply = "Không thể khởi tạo phiên trò chuyện. Vui lòng thử lại sau giây lát."
                };
            }

            var aiUser = await EnsureAiUserAsync();
            var currentUser = !string.IsNullOrWhiteSpace(userId) ? await _userManager.FindByIdAsync(userId) : null;
            string senderId = currentUser?.Id ?? (await EnsureGuestUserAsync()).Id;

            // 1. Lưu tin nhắn người dùng vào CSDL
            var userMsgEntity = new Message
            {
                ConversationId = conv.ConversationId,
                SenderId = senderId,
                Content = userMessage,
                SentAt = DateTime.Now
            };
            _context.Messages.Add(userMsgEntity);
            await _context.SaveChangesAsync();

            // 2. Phân tích nội dung và sinh câu trả lời thông minh
            var responseDto = await GenerateSmartResponseAsync(userMessage, currentUser, conv.ConversationId);

            // 3. Lưu câu trả lời của AI vào CSDL
            var aiMsgEntity = new Message
            {
                ConversationId = conv.ConversationId,
                SenderId = aiUser.Id,
                Content = responseDto.Reply,
                SentAt = DateTime.Now
            };
            _context.Messages.Add(aiMsgEntity);
            await _context.SaveChangesAsync();

            responseDto.ConversationId = conv.ConversationId;
            return responseDto;
        }

        private async Task<ChatResponseDto> GenerateSmartResponseAsync(string rawText, ApplicationUser? currentUser, int conversationId)
        {
            string cleanText = (rawText ?? string.Empty).Trim();
            string lowerText = cleanText.ToLower();

            var response = new ChatResponseDto
            {
                ConversationId = conversationId,
                QuickPrompts = new List<string>()
            };

            // CASE 1: Kiểm tra xem có mã đơn hàng cụ thể không (Regex: ORD...)
            var orderCodeMatch = Regex.Match(cleanText, @"ORD[\w\-]+", RegexOptions.IgnoreCase);
            if (orderCodeMatch.Success)
            {
                string targetOrderCode = orderCodeMatch.Value.ToUpper();
                return await HandleSpecificOrderInquiryAsync(targetOrderCode, currentUser);
            }

            // CASE 2: Tra cứu đơn hàng của tôi / tình trạng đơn hàng
            if (lowerText.Contains("đơn hàng của tôi") ||
                lowerText.Contains("đơn của tôi") ||
                lowerText.Contains("kiểm tra đơn") ||
                lowerText.Contains("tình trạng đơn") ||
                lowerText.Contains("lịch sử đơn") ||
                lowerText.Contains("tôi có bao nhiêu đơn") ||
                lowerText.Contains("danh sách đơn") ||
                lowerText.Contains("các đơn đang giao"))
            {
                return await HandleUserOrdersInquiryAsync(currentUser);
            }

            // CASE 3: Hỏi về cước phí / tính giá / báo giá
            if (lowerText.Contains("cước") ||
                lowerText.Contains("giá") ||
                lowerText.Contains("phí") ||
                lowerText.Contains("bao nhiêu tiền") ||
                lowerText.Contains("tính tiền") ||
                lowerText.Contains("báo giá") ||
                lowerText.Contains("chi phí") ||
                lowerText.Contains("ước tính") ||
                lowerText.Contains("bảng giá"))
            {
                return await HandleFeeEstimationInquiryAsync(cleanText, lowerText);
            }

            // CASE 4: Hỏi về danh mục loại hàng hóa / gửi được gì / quy cách đóng gói
            if (lowerText.Contains("loại hàng") ||
                lowerText.Contains("danh mục") ||
                lowerText.Contains("gửi được gì") ||
                lowerText.Contains("quy cách") ||
                lowerText.Contains("đóng gói") ||
                lowerText.Contains("mặt hàng") ||
                lowerText.Contains("kiện hàng") ||
                lowerText.Contains("hàng cấm") ||
                lowerText.Contains("dễ vỡ") ||
                lowerText.Contains("đồ điện tử") ||
                lowerText.Contains("laptop") ||
                lowerText.Contains("tivi"))
            {
                return await HandlePackageCategoryInquiryAsync();
            }

            // CASE 5: Hỏi về bưu cục / trạm trung chuyển / địa chỉ gửi hàng
            if (lowerText.Contains("bưu cục") ||
                lowerText.Contains("trạm") ||
                lowerText.Contains("chi nhánh") ||
                lowerText.Contains("địa chỉ") ||
                lowerText.Contains("ở đâu") ||
                lowerText.Contains("gần nhất") ||
                lowerText.Contains("bản đồ"))
            {
                return await HandleStationInquiryAsync();
            }

            // CASE 6: Hỏi về thời gian giao hàng, COD, bảo hiểm, hủy đơn
            if (lowerText.Contains("thời gian") ||
                lowerText.Contains("bao lâu") ||
                lowerText.Contains("mấy ngày") ||
                lowerText.Contains("thanh toán") ||
                lowerText.Contains("cod") ||
                lowerText.Contains("bảo hiểm") ||
                lowerText.Contains("đền bù") ||
                lowerText.Contains("hủy đơn") ||
                lowerText.Contains("hotline") ||
                lowerText.Contains("liên hệ"))
            {
                return HandlePoliciesAndFaqInquiry(lowerText);
            }

            // CASE 7: Chào hỏi / Hỏi trợ lý AI là ai
            if (lowerText.Contains("chào") ||
                lowerText.Contains("hello") ||
                lowerText.Contains("hi") ||
                lowerText.Contains("alo") ||
                lowerText.Contains("bạn là ai") ||
                lowerText.Contains("ai đấy") ||
                lowerText.Contains("giúp"))
            {
                string greetingName = !string.IsNullOrWhiteSpace(currentUser?.FullName) ? $" **{currentUser.FullName}**" : "";
                response.Reply = $"Xin chào{greetingName}! 👋 Em là **Trợ lý AI Thông Minh** của hệ thống vận chuyển **Tốc Độ Delivery**.\n\n" +
                                 $"Em có thể hỗ trợ anh/chị 24/7 về các vấn đề:\n" +
                                 $"• 📦 **Tra cứu đơn hàng của bạn** & hành trình bưu phẩm\n" +
                                 $"• 💰 **Tính cước phí dự kiến** theo khoảng cách và cân nặng\n" +
                                 $"• 🏷️ **Danh mục loại hàng hóa** & quy chuẩn đóng gói kiện hàng\n" +
                                 $"• 📍 **Tra cứu bưu cục & trạm trung chuyển** gần nhất\n" +
                                 $"• 📋 **Chính sách giao nhận, bảo hiểm & thời gian phát hàng**\n\n" +
                                 $"Anh/chị cần em hỗ trợ gì ngay bây giờ ạ?";

                response.QuickPrompts = new List<string>
                {
                    "📦 Đơn hàng của tôi",
                    "💰 Tính cước phí dự kiến",
                    "🏷️ Các loại hàng hóa nhận gửi",
                    "📍 Tìm bưu cục gần nhất",
                    "⏱️ Thời gian giao hàng bao lâu?"
                };
                return response;
            }

            // CASE 8: Fallback mặc định hỗ trợ thông minh
            response.Reply = $"Dạ em đã ghi nhận thông điệp của bạn: *\"{cleanText}\"*.\n\n" +
                             $"Để hỗ trợ bạn nhanh chóng và chính xác nhất, bạn có thể chọn một trong các thao tác bên dưới hoặc nhập thông tin cụ thể (ví dụ: *\"Kiểm tra đơn ORD...\"* hoặc *\"Tính cước 2kg đi 10km\"*):";

            response.QuickPrompts = new List<string>
            {
                "📦 Đơn hàng của tôi",
                "💰 Tính cước 2kg 10km",
                "🏷️ Xem danh mục loại hàng",
                "📍 Bưu cục gửi hàng ở đâu?",
                "📞 Hotline hỗ trợ 24/7"
            };

            return response;
        }

        private async Task<ChatResponseDto> HandleSpecificOrderInquiryAsync(string orderCode, ApplicationUser? currentUser)
        {
            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .Include(o => o.OrderStatusHistories)
                .Include(o => o.DeliveryStaff)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
            {
                return new ChatResponseDto
                {
                    Reply = $"🔍 Rất tiếc, em không tìm thấy đơn hàng nào có mã **{orderCode}** trong hệ thống.\n\n" +
                            $"Anh/chị vui lòng kiểm tra lại chính xác mã vận đơn (bắt đầu bằng `ORD...`) hoặc xem lại danh sách đơn hàng đã tạo trong tài khoản nhé!",
                    QuickPrompts = new List<string>
                    {
                        "📦 Xem các đơn của tôi",
                        "🚚 Tạo đơn hàng mới",
                        "📞 Gọi tổng đài hỗ trợ 1900 8888"
                    },
                    ActionUrl = "/DeliveryOrder/Index",
                    ActionText = "Danh sách đơn hàng của bạn"
                };
            }

            string badgeColor = order.CurrentStatus switch
            {
                "Chờ tiếp nhận" => "warning",
                "Đã nhận - Chưa giao" => "info",
                "Đang lấy hàng" => "primary",
                "Đang giao" => "primary",
                "Giao thành công" => "success",
                "Giao thất bại" => "danger",
                "Đã hủy" => "secondary",
                _ => "info"
            };

            var latestHistory = order.OrderStatusHistories.OrderByDescending(h => h.ChangedAt).FirstOrDefault();
            string historyInfo = latestHistory != null
                ? $"\n• 🕒 **Cập nhật mới nhất:** {latestHistory.ChangedAt:HH:mm dd/MM/yyyy} - *{latestHistory.Status}*"
                : "";

            string shipperInfo = order.DeliveryStaff != null
                ? $"\n• 🛵 **Shipper phụ trách:** {order.DeliveryStaff.FullName} (SĐT: {order.DeliveryStaff.PhoneNumber ?? "1900 8888"})"
                : "";

            string itemsText = string.Join(", ", order.OrderItems.Select(i => $"{i.Description} ({i.Quantity} cái, {i.Weight}kg)"));

            string reply = $"✅ **Thông tin chi tiết vận đơn {order.OrderCode}:**\n\n" +
                           $"• 📌 **Trạng thái hiện tại:** **{order.CurrentStatus}**\n" +
                           $"• 👤 **Người nhận:** {order.ReceiverName} ({order.ReceiverPhone})\n" +
                           $"• 📍 **Địa chỉ giao:** {order.ReceiverAddress}\n" +
                           $"• 📦 **Kiện hàng:** {(string.IsNullOrWhiteSpace(itemsText) ? "Bưu phẩm" : itemsText)}\n" +
                           $"• ⚡ **Hình thức:** {order.DeliveryMethod} ({order.DistanceKm} km)\n" +
                           $"• 💵 **Cước phí:** {order.TotalAmount:N0} VNĐ ({order.PaymentStatus}){shipperInfo}{historyInfo}\n\n" +
                           $"Bạn có thể bấm nút bên dưới để xem toàn bộ hành trình vận chuyển chi tiết trên bản đồ!";

            var orderDto = new OrderSummaryDto
            {
                OrderCode = order.OrderCode,
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                ReceiverAddress = order.ReceiverAddress,
                CurrentStatus = order.CurrentStatus,
                StatusBadgeClass = $"bg-{badgeColor}",
                TotalAmount = order.TotalAmount,
                DeliveryMethod = order.DeliveryMethod,
                CreatedAt = order.CreatedAt,
                ItemsCount = order.OrderItems.Count,
                LatestTrackingNote = latestHistory?.Status
            };

            return new ChatResponseDto
            {
                Reply = reply,
                Orders = new List<OrderSummaryDto> { orderDto },
                ActionUrl = $"/DeliveryOrder/Details?id={order.OrderCode}",
                ActionText = "Xem chi tiết & Theo dõi đơn hàng này",
                QuickPrompts = new List<string>
                {
                    "📦 Xem tất cả đơn của tôi",
                    "💰 Tính cước đơn khác",
                    "📍 Bưu cục gửi hàng"
                }
            };
        }

        private async Task<ChatResponseDto> HandleUserOrdersInquiryAsync(ApplicationUser? currentUser)
        {
            if (currentUser == null)
            {
                return new ChatResponseDto
                {
                    Reply = "🔒 Để xem danh sách các đơn hàng của riêng bạn, bạn vui lòng **Đăng nhập** tài khoản nhé!\n\n" +
                            "👉 Nếu bạn đã có mã vận đơn cụ thể (ví dụ: `ORD...`), bạn chỉ cần nhắn trực tiếp mã đơn vào đây, em sẽ tra cứu trạng thái ngay giúp bạn mà không cần đăng nhập!",
                    ActionUrl = "/Account/Login",
                    ActionText = "Đăng nhập tài khoản",
                    QuickPrompts = new List<string>
                    {
                        "💰 Tính cước phí dự kiến",
                        "🏷️ Xem danh mục loại hàng",
                        "📍 Tìm bưu cục gần nhất"
                    }
                };
            }

            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Include(o => o.OrderStatusHistories)
                .Where(o => o.CustomerId == currentUser.Id)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            if (!orders.Any())
            {
                return new ChatResponseDto
                {
                    Reply = $"Chào bạn **{currentUser.FullName}**! Hiện tại tài khoản của bạn chưa có đơn hàng nào được tạo trên hệ thống.\n\n" +
                            "Bạn có muốn tạo ngay một đơn giao hàng hỏa tốc trong ngày không ạ?",
                    ActionUrl = "/DeliveryOrder/Create",
                    ActionText = "Tạo đơn giao hàng mới ngay",
                    QuickPrompts = new List<string>
                    {
                        "🚚 Tạo yêu cầu gửi hàng",
                        "💰 Tính cước phí dự kiến",
                        "🏷️ Các loại hàng được gửi"
                    }
                };
            }

            var orderDtos = new List<OrderSummaryDto>();
            var lines = new List<string>();

            lines.Add($"📦 **Danh sách 5 đơn hàng gần nhất của bạn ({currentUser.FullName}):**\n");

            int idx = 1;
            foreach (var o in orders)
            {
                string badge = o.CurrentStatus switch
                {
                    "Giao thành công" => "🟢 Giao thành công",
                    "Đang giao" => "🔵 Đang giao",
                    "Đang lấy hàng" => "🟡 Đang lấy hàng",
                    "Chờ tiếp nhận" => "🟠 Chờ tiếp nhận",
                    "Đã hủy" => "⚪ Đã hủy",
                    _ => $"🔷 {o.CurrentStatus}"
                };

                lines.Add($"**{idx}. [{o.OrderCode}]** - {badge}");
                lines.Add($"   • Đến: {o.ReceiverName} ({o.ReceiverAddress})");
                lines.Add($"   • Cước: {o.TotalAmount:N0} đ | Ngày tạo: {o.CreatedAt:dd/MM/yyyy HH:mm}\n");

                orderDtos.Add(new OrderSummaryDto
                {
                    OrderCode = o.OrderCode,
                    ReceiverName = o.ReceiverName,
                    ReceiverPhone = o.ReceiverPhone,
                    ReceiverAddress = o.ReceiverAddress,
                    CurrentStatus = o.CurrentStatus,
                    TotalAmount = o.TotalAmount,
                    DeliveryMethod = o.DeliveryMethod,
                    CreatedAt = o.CreatedAt,
                    ItemsCount = o.OrderItems.Count
                });
                idx++;
            }

            lines.Add("💡 *Bạn có thể nhắn mã đơn cụ thể (vd: " + orders[0].OrderCode + ") để em kiểm tra lộ trình chi tiết nhé!*");

            return new ChatResponseDto
            {
                Reply = string.Join("\n", lines),
                Orders = orderDtos,
                ActionUrl = "/DeliveryOrder/Index",
                ActionText = "Xem tất cả đơn hàng trong tài khoản",
                QuickPrompts = new List<string>
                {
                    $"Kiểm tra đơn {orders[0].OrderCode}",
                    "🚚 Tạo đơn giao mới",
                    "💰 Tính cước phí dự kiến"
                }
            };
        }

        private async Task<ChatResponseDto> HandleFeeEstimationInquiryAsync(string rawText, string lowerText)
        {
            // Trích xuất cân nặng
            decimal weight = 0;
            var weightMatch = Regex.Match(lowerText, @"(\d+(?:[.,]\d+)?)\s*(kg|kí|ký|kilogram|gram|\bg\b)", RegexOptions.IgnoreCase);
            if (weightMatch.Success)
            {
                string val = weightMatch.Groups[1].Value.Replace(',', '.');
                string unit = weightMatch.Groups[2].Value.ToLower();
                if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedWeight))
                {
                    if (unit == "gram" || unit == "g")
                    {
                        weight = parsedWeight / 1000m;
                    }
                    else
                    {
                        weight = parsedWeight;
                    }
                }
            }

            // Trích xuất khoảng cách
            decimal distance = 0;
            var distMatch = Regex.Match(lowerText, @"(\d+(?:[.,]\d+)?)\s*(?:km|cây|kilomet|kilômét)");
            if (distMatch.Success)
            {
                string val = distMatch.Groups[1].Value.Replace(',', '.');
                if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedDist))
                {
                    distance = parsedDist;
                }
            }

            // Trích xuất phương thức giao
            string method = "Thường";
            if (lowerText.Contains("nhanh") || lowerText.Contains("hỏa tốc") || lowerText.Contains("gấp"))
            {
                method = "Nhanh";
            }

            // Nếu người dùng đã cung cấp cả cân nặng hoặc khoảng cách
            if (weight > 0 || distance > 0)
            {
                if (weight <= 0) weight = 2.0m; // Default giả định hợp lý nếu thiếu
                if (distance <= 0) distance = 5.0m;

                var feeResult = await EstimateFeeDirectAsync(method, weight, distance);
                if (feeResult != null)
                {
                    string surchargeNote = "";
                    if (feeResult.WeatherFee > 0)
                    {
                        surchargeNote += $"\n   • 🌧️ Phụ phí thời tiết ({feeResult.WeatherDescription}): +{feeResult.WeatherFee:N0} đ";
                    }
                    if (feeResult.TrafficFee > 0)
                    {
                        surchargeNote += $"\n   • 🚦 Phụ phí giao thông/giờ cao điểm ({feeResult.TrafficDescription}): +{feeResult.TrafficFee:N0} đ";
                    }

                    string reply = $"💰 **BẢNG TÍNH CƯỚC PHÍ DỰ KIẾN THEO HỆ THỐNG:**\n\n" +
                                   $"• 📦 **Khối lượng:** {weight:N1} kg\n" +
                                   $"• 📍 **Khoảng cách:** {distance:N1} km\n" +
                                   $"• ⚡ **Gói dịch vụ:** Giao hàng {method}\n" +
                                   $"----------------------------------\n" +
                                   $"• 💵 **Cước cự ly cơ bản:** {feeResult.BaseShippingFee:N0} đ\n" +
                                   $"• ⚖️ **Phụ phí khối lượng:** {feeResult.WeightFee:N0} đ{surchargeNote}\n" +
                                   $"----------------------------------\n" +
                                   $"👉 **TỔNG CƯỚC DỰ KIẾN:** **{feeResult.TotalPrice:N0} VNĐ**\n\n" +
                                   $"*(Mức cước đã bao gồm bảo hiểm cơ bản và tính toán theo điều kiện giao thông thời gian thực)*";

                    return new ChatResponseDto
                    {
                        Reply = reply,
                        EstimatedFee = feeResult,
                        ActionUrl = "/DeliveryOrder/Create",
                        ActionText = "Gửi đơn hàng với mức cước này",
                        QuickPrompts = new List<string>
                        {
                            "Gửi hàng hỏa tốc",
                            "Tính cước 5kg 15km",
                            "🏷️ Các loại hàng nhận gửi",
                            "📦 Đơn hàng của tôi"
                        }
                    };
                }
            }

            // Nếu người dùng chưa cung cấp đủ tham số
            return new ChatResponseDto
            {
                Reply = "💰 **Tính cước phí dự kiến cực nhanh:**\n\n" +
                        "Hệ thống tính cước của **Tốc Độ Delivery** được tính tự động dựa trên:\n" +
                        "1. **Cự ly di chuyển (km)** giữa địa chỉ người gửi và người nhận\n" +
                        "2. **Khối lượng bưu phẩm (kg)**\n" +
                        "3. **Gói giao hàng:** *Giao Tiêu Chuẩn (Thường)* hoặc *Giao Hỏa Tốc (Nhanh)*\n\n" +
                        "👉 **Bạn có thể thử nhắn:**\n" +
                        "• *\"Tính cước 2kg đi 8km\"*\n" +
                        "• *\"Gửi hàng nhanh 5kg khoảng cách 12km hết bao nhiêu?\"*\n\n" +
                        "Hoặc bạn có thể bấm vào công cụ ước tính giá trên trang chủ để tự động đo khoảng cách theo địa chỉ!",
                ActionUrl = "/PriceTable/Index",
                ActionText = "Xem bảng giá bậc thang chi tiết",
                QuickPrompts = new List<string>
                {
                    "Tính cước 1kg 5km thường",
                    "Tính cước 3kg 10km nhanh",
                    "Tính cước 5kg 20km",
                    "🏷️ Xem danh mục loại hàng"
                }
            };
        }

        private async Task<ChatResponseDto> HandlePackageCategoryInquiryAsync()
        {
            var activeTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.PackageTypeId)
                .ToListAsync();

            var lines = new List<string>();
            lines.Add("🏷️ **DANH MỤC CÁC LOẠI HÀNG HÓA NHẬN GIAO:**\n");
            lines.Add("Tốc Độ Delivery nhận vận chuyển hầu hết các loại hàng hóa thông dụng theo danh mục chuẩn:");

            foreach (var type in activeTypes)
            {
                string desc = !string.IsNullOrWhiteSpace(type.Description) ? $" - {type.Description}" : "";
                lines.Add($"• **{type.TypeName}**{desc}");
            }

            lines.Add("\n🛡️ **Lưu ý khi đóng gói hàng hóa:**");
            lines.Add("• **Hàng dễ vỡ (gốm sứ, màn hình, kính):** Vui lòng chèn mút xốp chống va đập, dán nhãn 'HÀNG DỄ VỠ'.");
            lines.Add("• **Hàng điện tử (Laptop, CPU, Điện thoại):** Được bọc chống sốc và niêm phong bằng tem chuyên dụng.");
            lines.Add("• **Hàng cấm:** Hệ thống nghiêm cấm gửi chất cháy nổ, hàng quốc cấm, tiền mặt hoặc động vật sống.");

            return new ChatResponseDto
            {
                Reply = string.Join("\n", lines),
                ActionUrl = "/DeliveryOrder/Create",
                ActionText = "Đặt đơn giao hàng ngay",
                QuickPrompts = new List<string>
                {
                    "💰 Tính cước phí dự kiến",
                    "📦 Đơn hàng của tôi",
                    "📍 Tìm bưu cục gần nhất",
                    "🛡️ Chính sách bảo hiểm hàng hóa"
                }
            };
        }

        private async Task<ChatResponseDto> HandleStationInquiryAsync()
        {
            var stations = await _context.MapStations
                .Where(s => s.IsActive)
                .Take(5)
                .ToListAsync();

            var lines = new List<string>();
            lines.Add("📍 **MẠNG LƯỚI BƯU CỤC & TRẠM TRUNG CHUYỂN TỐC ĐỘ DELIVERY:**\n");
            lines.Add("Quý khách có thể đến gửi hàng trực tiếp hoặc liên hệ nhận bưu kiện tại các điểm:");

            foreach (var st in stations)
            {
                lines.Add($"• 🏢 **{st.StationName}**\n  Địa chỉ: {st.Address}");
            }

            lines.Add("\n🕒 **Giờ mở cửa:** 24/7 (Cả Thứ Bảy, Chủ Nhật và các ngày Lễ).");
            lines.Add("Hotline điều phối trạm: **1900 8888**");

            return new ChatResponseDto
            {
                Reply = string.Join("\n", lines),
                ActionUrl = "/Home/Stations",
                ActionText = "Xem bản đồ bưu cục toàn quốc",
                QuickPrompts = new List<string>
                {
                    "💰 Tính cước phí dự kiến",
                    "🏷️ Xem danh mục loại hàng",
                    "📦 Đơn hàng của tôi"
                }
            };
        }

        private ChatResponseDto HandlePoliciesAndFaqInquiry(string lowerText)
        {
            if (lowerText.Contains("thời gian") || lowerText.Contains("bao lâu") || lowerText.Contains("mấy ngày"))
            {
                return new ChatResponseDto
                {
                    Reply = "⏱️ **THỜI GIAN GIAO HÀNG DỰ KIẾN:**\n\n" +
                            "• ⚡ **Giao Hỏa Tốc (Nội thành):** Từ **1 - 3 giờ** kể từ khi shipper nhận hàng. Không lưu kho trung gian, giao thẳng tới người nhận.\n" +
                            "• 🚚 **Giao Tiêu Chuẩn:** Trong ngày (nội tỉnh) hoặc **24 - 48 giờ** (liên tỉnh, ngoại thành).\n\n" +
                            "Hệ thống cập nhật tọa độ check-in realtime của Shipper nên bạn hoàn toàn yên tâm theo dõi sát sao lộ trình!",
                    QuickPrompts = new List<string>
                    {
                        "💰 Tính cước giao hỏa tốc",
                        "📦 Kiểm tra đơn của tôi",
                        "📍 Tìm bưu cục gần nhất"
                    }
                };
            }

            if (lowerText.Contains("thanh toán") || lowerText.Contains("cod"))
            {
                return new ChatResponseDto
                {
                    Reply = "💳 **HÌNH THỨC THANH TOÁN ĐA DẠNG & TIỆN LỢI:**\n\n" +
                            "1. 💵 **Thanh toán tiền mặt khi nhận hàng (COD):** Người nhận kiểm tra bưu kiện và thanh toán cho Shipper.\n" +
                            "2. 📲 **Thanh toán trực tuyến:** Quét mã QR chuyển khoản, thẻ ATM nội địa hoặc thẻ Quốc tế (Visa/Mastercard).\n\n" +
                            "Tiền COD thu hộ sẽ được hệ thống đối soát và chuyển tự động vào tài khoản người gửi nhanh chóng!",
                    QuickPrompts = new List<string>
                    {
                        "🚚 Tạo đơn giao ngay",
                        "💰 Tính cước phí dự kiến",
                        "📦 Đơn hàng của tôi"
                    }
                };
            }

            if (lowerText.Contains("bảo hiểm") || lowerText.Contains("đền bù"))
            {
                return new ChatResponseDto
                {
                    Reply = "🛡️ **CHÍNH SÁCH BẢO HIỂM & ĐỀN BÙ 100%:**\n\n" +
                            "• Mọi kiện hàng gửi qua **Tốc Độ Delivery** đều được bảo hiểm mặc định theo giá trị khai báo.\n" +
                            "• Trường hợp hàng hóa bị móp méo, hư hỏng hoặc thất lạc do lỗi vận chuyển, hệ thống cam kết **đền bù lên tới 100% giá trị kiện hàng** trong vòng 24 - 48 giờ sau khi xác nhận biên bản.\n" +
                            "• Đối với hàng điện tử giá trị cao (Laptop, Điện thoại), shipper bắt buộc chụp ảnh đối chứng khi giao nhận.",
                    QuickPrompts = new List<string>
                    {
                        "🏷️ Xem danh mục loại hàng",
                        "💰 Tính cước phí dự kiến",
                        "📞 Tổng đài khiếu nại: 1900 8888"
                    }
                };
            }

            // Mặc định hotline & liên hệ
            return new ChatResponseDto
            {
                Reply = "📞 **THÔNG TIN LIÊN HỆ & TỔNG ĐÀI HỖ TRỢ:**\n\n" +
                        "• ☎️ **Tổng đài CSKH 24/7:** **1900 8888** (Miễn phí cước gọi)\n" +
                        "• 📧 **Email tiếp nhận:** hotro@tocdodelivery.vn\n" +
                        "• 🏢 **Trụ sở chính:** TP. Hồ Chí Minh, Việt Nam\n\n" +
                        "Đội ngũ nhân viên tiếp nhận và kỹ thuật viên luôn sẵn sàng giải đáp mọi thắc mắc của bạn!",
                QuickPrompts = new List<string>
                {
                    "📦 Đơn hàng của tôi",
                    "💰 Tính cước phí dự kiến",
                    "🏷️ Danh mục hàng nhận gửi"
                }
            };
        }
    }
}
