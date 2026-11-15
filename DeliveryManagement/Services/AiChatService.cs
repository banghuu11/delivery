using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeliveryManagement.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPricingService _pricingService;
        private readonly ILogger<AiChatService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string AiEmail = "ai-assistant@delivery.com";
        private const string GuestEmail = "guest@delivery.com";

        public AiChatService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPricingService pricingService,
            ILogger<AiChatService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _pricingService = pricingService;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
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

            // 2. Phân tích nội dung và sinh câu trả lời thông minh, tự nhiên
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

            // 1. Nếu có cấu hình Gemini API Key -> Thử gọi Gemini trước để có văn phong đàm thoại siêu tự nhiên
            string? geminiReply = await TryCallGeminiAsync(cleanText, currentUser);
            if (!string.IsNullOrWhiteSpace(geminiReply))
            {
                return new ChatResponseDto
                {
                    ConversationId = conversationId,
                    Reply = geminiReply,
                    QuickPrompts = new List<string>
                    {
                        "📦 Đơn hàng của tôi",
                        "💰 Tính cước phí dự kiến",
                        "⏱️ Thời gian giao hàng",
                        "📍 Tìm bưu cục gần nhất"
                    }
                };
            }

            // 2. Chạy bộ não Local Conversational AI thông minh, tự nhiên và giàu nghiệp vụ
            return await GenerateLocalSmartResponseAsync(cleanText, currentUser, conversationId);
        }

        private async Task<string?> TryCallGeminiAsync(string userMessage, ApplicationUser? currentUser)
        {
            try
            {
                string? apiKey = _configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return null;
                }

                var activeTypes = await _context.PackageTypes
                    .Where(p => p.IsActive)
                    .Select(p => p.TypeName)
                    .ToListAsync();

                var client = _httpClientFactory.CreateClient();
                string modelName = "gemini-1.5-flash";
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

                string userName = currentUser?.FullName ?? "quý khách";
                string systemContext = $"Bạn là Trợ lý AI Tốc Độ của công ty vận chuyển Tốc Độ Delivery (hệ thống giao hàng nhanh tại Việt Nam). " +
                                       $"Khách hàng đang nhắn tin với bạn là: {userName}. " +
                                       $"Danh mục loại hàng hóa công ty nhận vận chuyển: {string.Join(", ", activeTypes)}. " +
                                       $"Dịch vụ: Giao tiêu chuẩn (trong ngày / 24-48h), Giao hỏa tốc (1-3h). Hoạt động 24/7. Hỗ trợ xem hàng trước khi nhận, COD toàn quốc, đền bù 100% nếu hư hỏng. Hotline CSKH: 1900 8888. " +
                                       $"Hãy trả lời khách hàng bằng tiếng Việt tự nhiên, ân cần, xưng 'em' và gọi 'anh/chị', trả lời thẳng vào trọng tâm câu hỏi của khách hàng, không máy móc, không lặp lại câu máy móc.";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = $"{systemContext}\n\nKhách hàng hỏi: \"{userMessage}\"\nHãy trả lời một cách tự nhiên và giải đáp thắc mắc ngay:" }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 800
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    string jsonStr = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        if (parts.GetArrayLength() > 0)
                        {
                            return parts[0].GetProperty("text").GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể kết nối Gemini API, chuyển sang Local AI Engine.");
            }

            return null;
        }

        private async Task<ChatResponseDto> GenerateLocalSmartResponseAsync(string cleanText, ApplicationUser? currentUser, int conversationId)
        {
            string lowerText = cleanText.ToLower();
            string userDisplayName = !string.IsNullOrWhiteSpace(currentUser?.FullName) ? currentUser.FullName : "anh/chị";

            var response = new ChatResponseDto
            {
                ConversationId = conversationId,
                QuickPrompts = new List<string>()
            };

            // =========================================================================
            // 1. TRA CỨU ĐƠN HÀNG CỤ THỂ QUA MÃ VẬN ĐƠN (ORD...)
            // =========================================================================
            var orderCodeMatch = Regex.Match(cleanText, @"ORD[\w\-]+", RegexOptions.IgnoreCase);
            if (orderCodeMatch.Success)
            {
                string targetOrderCode = orderCodeMatch.Value.ToUpper();
                return await HandleSpecificOrderInquiryAsync(targetOrderCode, currentUser);
            }

            // =========================================================================
            // 2. TRA CỨU DANH SÁCH ĐƠN HÀNG CỦA TÔI / TÌNH TRẠNG ĐƠN HÀNG
            // =========================================================================
            if (lowerText.Contains("đơn của tôi") ||
                lowerText.Contains("đơn hàng của tôi") ||
                lowerText.Contains("kiểm tra đơn") ||
                lowerText.Contains("tra cứu đơn") ||
                lowerText.Contains("tình trạng đơn") ||
                lowerText.Contains("danh sách đơn") ||
                lowerText.Contains("lịch sử đơn") ||
                lowerText.Contains("đơn tôi đã đặt") ||
                lowerText.Contains("tôi có đơn nào") ||
                lowerText.Contains("đơn đang giao"))
            {
                return await HandleUserOrdersInquiryAsync(currentUser);
            }

            // =========================================================================
            // 3. TÍNH CƯỚC PHÍ DỰ KIẾN / BÁO GIÁ VẬN CHUYỂN
            // =========================================================================
            if (lowerText.Contains("cước") ||
                lowerText.Contains("giá ship") ||
                lowerText.Contains("phí ship") ||
                lowerText.Contains("tiền ship") ||
                lowerText.Contains("bao nhiêu tiền") ||
                lowerText.Contains("tính tiền") ||
                lowerText.Contains("báo giá") ||
                lowerText.Contains("bảng giá") ||
                lowerText.Contains("chi phí") ||
                lowerText.Contains("ước tính phí") ||
                lowerText.Contains("giá cước") ||
                lowerText.Contains("phí giao hàng") ||
                Regex.IsMatch(lowerText, @"\d+\s*(kg|kí|ký|km|cây)"))
            {
                return await HandleFeeEstimationInquiryAsync(cleanText, lowerText);
            }

            // =========================================================================
            // 4. THỜI GIAN GIAO HÀNG / KHI NÀO TỚI / GIAO MẤY NGÀY / BAN ĐÊM / NGÀY LỄ
            // =========================================================================
            if (lowerText.Contains("thời gian") ||
                lowerText.Contains("bao lâu") ||
                lowerText.Contains("mấy ngày") ||
                lowerText.Contains("mấy giờ") ||
                lowerText.Contains("khi nào tới") ||
                lowerText.Contains("khi nào nhận") ||
                lowerText.Contains("ban đêm") ||
                lowerText.Contains("buổi tối") ||
                lowerText.Contains("chủ nhật") ||
                lowerText.Contains("thứ 7") ||
                lowerText.Contains("ngày lễ") ||
                lowerText.Contains("giờ hành chính") ||
                lowerText.Contains("hẹn giờ"))
            {
                response.Reply = $"Dạ chào {userDisplayName}! Về **thời gian giao hàng**, hệ thống **Tốc Độ Delivery** phục vụ 24/7 với cam kết tiến độ cụ thể như sau ạ:\n\n" +
                                 $"• ⚡ **Giao Hỏa Tốc (Nội thành):** Chỉ từ **1 - 3 giờ**! Ngay khi shipper lấy hàng sẽ giao thẳng trực tiếp đến người nhận, hoàn toàn không qua kho trung chuyển.\n" +
                                 $"• 🚚 **Giao Tiêu Chuẩn (Thường):**\n" +
                                 $"   - Nội thành / nội tỉnh: Giao ngay **trong ngày** (trong vòng 6 - 12 giờ).\n" +
                                 $"   - Liên tỉnh & ngoại thành: Từ **24 - 48 giờ** làm việc.\n" +
                                 $"• 🌙 **Giao ban đêm, Chủ nhật & Ngày lễ:** Bên em vẫn hoạt động bình thường kể cả cuối tuần và buổi tối. Anh/chị cũng có thể ghi chú thời gian người nhận có thể nhận hàng khi tạo đơn ạ!\n\n" +
                                 $"Anh/chị đang cần gửi hàng hỏa tốc trong ngày hay giao tiêu chuẩn ạ?";

                response.QuickPrompts = new List<string>
                {
                    "⚡ Tính cước giao hỏa tốc",
                    "🚚 Tạo đơn giao ngay",
                    "📦 Kiểm tra đơn của tôi",
                    "💰 Bảng giá giao hàng"
                };
                response.ActionUrl = "/DeliveryOrder/Create";
                response.ActionText = "Tạo đơn giao hàng ngay";
                return response;
            }

            // =========================================================================
            // 5. QUY TRÌNH GỬI HÀNG / CÁCH GỬI / SHIPPER ĐẾN LẤY TẬN NƠI HAY RA BƯU CỤC
            // =========================================================================
            if (lowerText.Contains("làm sao để gửi") ||
                lowerText.Contains("cách gửi") ||
                lowerText.Contains("muốn gửi") ||
                lowerText.Contains("quy trình gửi") ||
                lowerText.Contains("làm thế nào để tạo đơn") ||
                lowerText.Contains("tạo đơn thế nào") ||
                lowerText.Contains("shipper đến lấy") ||
                lowerText.Contains("đến tận nhà") ||
                lowerText.Contains("lấy hàng tận nơi") ||
                lowerText.Contains("đến lấy hàng"))
            {
                response.Reply = $"Dạ gửi hàng qua **Tốc Độ Delivery** cực kỳ đơn giản và tiện lợi với 2 cách sau ạ {userDisplayName}:\n\n" +
                                 $"1. 🛵 **Shipper đến lấy hàng tận nơi (Miễn phí):**\n" +
                                 $"   - Anh/chị chỉ cần bấm vào **\"Tạo đơn ngay\"** trên website, điền thông tin địa chỉ gửi, người nhận và khối lượng hàng.\n" +
                                 $"   - Shipper phụ trách khu vực sẽ liên hệ và đến tận nhà/cửa hàng của mình để lấy bưu phẩm chỉ sau 15 - 30 phút!\n\n" +
                                 $"2. 🏢 **Gửi trực tiếp tại Bưu cục / Trạm trung chuyển:**\n" +
                                 $"   - Anh/chị có thể mang hàng trực tiếp đến bất kỳ bưu cục nào gần nhất của Tốc Độ Delivery để nhân viên cân đo, dán mã vận đơn và gửi đi ngay.\n\n" +
                                 $"Anh/chị muốn tạo đơn shipper đến lấy tận nhà hay muốn tìm bưu cục gần mình nhất ạ?";

                response.QuickPrompts = new List<string>
                {
                    "🚚 Tạo đơn giao tận nơi",
                    "📍 Tìm bưu cục gần nhất",
                    "💰 Tính cước dự kiến",
                    "🏷️ Xem các loại hàng nhận gửi"
                };
                response.ActionUrl = "/DeliveryOrder/Create";
                response.ActionText = "Tạo đơn giao hàng ngay";
                return response;
            }

            // =========================================================================
            // 6. CHÍNH SÁCH ĐỒNG KIỂM / CHO XEM HÀNG TRƯỚC KHI NHẬN
            // =========================================================================
            if (lowerText.Contains("xem hàng") ||
                lowerText.Contains("cho xem hàng") ||
                lowerText.Contains("kiểm tra hàng") ||
                lowerText.Contains("đồng kiểm") ||
                lowerText.Contains("mở hàng"))
            {
                response.Reply = $"Dạ vâng, bên em **HOÀN TOÀN HỖ TRỢ ĐỒNG KIỂM (XEM HÀNG)** trước khi nhận ạ! 📦✨\n\n" +
                                 $"• Khi shipper giao đến, người nhận được quyền kiểm tra ngoại quan bưu kiện, đúng chủng loại, nguyên vẹn tem niêm phong trước khi thanh toán tiền COD.\n" +
                                 $"• Nếu hàng hóa có dấu hiệu móp méo, vỡ hỏng do vận chuyển, người nhận có thể từ chối nhận hàng và shipper sẽ lập biên bản hoàn hàng về người gửi an toàn.\n\n" +
                                 $"Chính sách này giúp cả người gửi và người nhận đều hoàn toàn an tâm khi giao dịch ạ!";

                response.QuickPrompts = new List<string>
                {
                    "🛡️ Chính sách bảo hiểm & đền bù",
                    "💵 Tiền thu hộ COD",
                    "🚚 Tạo đơn giao hàng ngay"
                };
                return response;
            }

            // =========================================================================
            // 7. CHÍNH SÁCH THANH TOÁN, TIỀN THU HỘ COD
            // =========================================================================
            if (lowerText.Contains("cod") ||
                lowerText.Contains("thu hộ") ||
                lowerText.Contains("tiền cod") ||
                lowerText.Contains("đối soát") ||
                lowerText.Contains("chuyển khoản") ||
                lowerText.Contains("quẹt thẻ") ||
                lowerText.Contains("thanh toán bằng gì") ||
                lowerText.Contains("mã qr"))
            {
                response.Reply = $"Dạ về **thanh toán & thu hộ COD**, Tốc Độ Delivery hỗ trợ cực kỳ tiện lợi cho {userDisplayName} ạ:\n\n" +
                                 $"1. 💵 **Dịch vụ thu hộ COD:**\n" +
                                 $"   - Shipper sẽ thu đúng số tiền COD từ người nhận khi giao hàng thành công.\n" +
                                 $"   - Tiền COD sẽ được hệ thống tự động đối soát và chuyển khoản về tài khoản ngân hàng của anh/chị ngay trong ngày hoặc ngày làm việc tiếp theo.\n\n" +
                                 $"2. 💳 **Phương thức thanh toán:**\n" +
                                 $"   - Thanh toán tiền mặt trực tiếp cho Shipper.\n" +
                                 $"   - Quét mã VietQR chuyển khoản ngân hàng nhanh chóng.\n" +
                                 $"   - Thanh toán trực tuyến qua thẻ ATM/Visa/Mastercard.\n\n" +
                                 $"Anh/chị cần em hỗ trợ thêm thông tin gì về thanh toán nữa không ạ?";

                response.QuickPrompts = new List<string>
                {
                    "🚚 Tạo đơn có thu COD",
                    "💰 Tính cước phí dự kiến",
                    "📦 Xem đơn hàng của tôi"
                };
                return response;
            }

            // =========================================================================
            // 8. ĐỔI ĐỊA CHỈ, SỬA SĐT NGƯỜI NHẬN HOẶC HỦY ĐƠN HÀNG
            // =========================================================================
            if (lowerText.Contains("đổi địa chỉ") ||
                lowerText.Contains("sửa địa chỉ") ||
                lowerText.Contains("sai địa chỉ") ||
                lowerText.Contains("đổi số điện thoại") ||
                lowerText.Contains("sai số") ||
                lowerText.Contains("sửa số điện thoại") ||
                lowerText.Contains("đổi thông tin") ||
                lowerText.Contains("hủy đơn") ||
                lowerText.Contains("hủy đơn hàng") ||
                lowerText.Contains("không gửi nữa"))
            {
                response.Reply = $"Dạ nếu {userDisplayName} cần **thay đổi thông tin hoặc hủy đơn hàng**, anh/chị thao tác như sau giúp em nhé:\n\n" +
                                 $"• 🔄 **Đơn ở trạng thái \"Chờ tiếp nhận\" hoặc \"Đã nhận\":**\n" +
                                 $"   - Anh/chị vào mục **[Đơn hàng của tôi](/DeliveryOrder/Index)** -> Chọn đơn hàng cần chỉnh sửa -> Bấm **Cập nhật** địa chỉ/SĐT hoặc bấm **Hủy đơn** trực tiếp trên web.\n\n" +
                                 $"• 🛵 **Đơn đang ở trạng thái \"Đang lấy hàng\" hoặc \"Đang giao\":**\n" +
                                 $"   - Shipper đã nhận bưu kiện nên để cập nhật địa chỉ mới kịp thời, anh/chị hãy gọi ngay tổng đài khẩn cấp **1900 8888** hoặc nhắn trực tiếp mã đơn (ví dụ: `ORD...`) vào đây, bên em sẽ điều phối liên hệ shipper ngay lập tức ạ!\n\n" +
                                 $"Anh/chị có mã đơn cụ thể cần điều chỉnh không ạ? Nhắn em hỗ trợ ngay nhé!";

                response.QuickPrompts = new List<string>
                {
                    "📦 Xem các đơn của tôi",
                    "📞 Hotline khẩn cấp 1900 8888",
                    "🚚 Tạo đơn giao mới"
                };
                response.ActionUrl = "/DeliveryOrder/Index";
                response.ActionText = "Vào quản lý đơn hàng của bạn";
                return response;
            }

            // =========================================================================
            // 9. KHIẾU NẠI, SỰ CỐ, SHIPPER, BẢO HIỂM & ĐỀN BÙ
            // =========================================================================
            if (lowerText.Contains("khiếu nại") ||
                lowerText.Contains("hàng bị vỡ") ||
                lowerText.Contains("hư hỏng") ||
                lowerText.Contains("bị rách") ||
                lowerText.Contains("thất lạc") ||
                lowerText.Contains("mất hàng") ||
                lowerText.Contains("đền bù") ||
                lowerText.Contains("bồi thường") ||
                lowerText.Contains("bảo hiểm") ||
                lowerText.Contains("thái độ shipper") ||
                lowerText.Contains("chưa thấy shipper") ||
                lowerText.Contains("sao lâu thế") ||
                lowerText.Contains("giao trễ") ||
                lowerText.Contains("chậm quá"))
            {
                response.Reply = $"Dạ em rất tiếc và thành thật xin lỗi {userDisplayName} vì sự bất tiện này ạ! 🥺\n\n" +
                                 $"**Tốc Độ Delivery luôn đặt quyền lợi của khách hàng lên hàng đầu:**\n" +
                                 $"• 🛡️ **Chính sách bồi thường 100%:** Đối với các đơn hàng bị hư hỏng, móp méo hoặc thất lạc do lỗi vận chuyển, hệ thống cam kết đền bù lên tới 100% giá trị bưu kiện trong vòng 24 - 48 giờ sau khi tiếp nhận biên bản.\n" +
                                 $"• 🛵 **Xử lý sự cố Shipper & Giao trễ:** Bộ phận kiểm soát chất lượng sẽ kiểm tra định vị realtime của shipper phụ trách đơn và đốc thúc giao ngay, hoặc điều phối shipper khác thay thế.\n\n" +
                                 $"👉 Anh/chị hãy nhắn giúp em **Mã vận đơn (ORD...)** hoặc gọi trực tiếp đến tổng đài CSKH ưu tiên **1900 8888** (miễn cước) để chuyên viên hỗ trợ xử lý tức thì cho mình nhé!";

                response.QuickPrompts = new List<string>
                {
                    "📦 Kiểm tra đơn của tôi",
                    "📞 Tổng đài khiếu nại: 1900 8888",
                    "🛡️ Chi tiết chính sách bảo hiểm"
                };
                return response;
            }

            // =========================================================================
            // 10. DANH MỤC LOẠI HÀNG HÓA NHẬN GỬI & QUY CÁCH ĐÓNG GÓI CHI TIẾT
            // =========================================================================
            if (lowerText.Contains("loại hàng") ||
                lowerText.Contains("danh mục") ||
                lowerText.Contains("gửi được gì") ||
                lowerText.Contains("mặt hàng") ||
                lowerText.Contains("kiện hàng") ||
                lowerText.Contains("đóng gói") ||
                lowerText.Contains("hàng cấm") ||
                lowerText.Contains("đồ điện tử") ||
                lowerText.Contains("laptop") ||
                lowerText.Contains("điện thoại") ||
                lowerText.Contains("dễ vỡ") ||
                lowerText.Contains("gốm sứ") ||
                lowerText.Contains("thủy tinh") ||
                lowerText.Contains("đồ ăn") ||
                lowerText.Contains("thực phẩm") ||
                lowerText.Contains("bánh kem") ||
                lowerText.Contains("quần áo") ||
                lowerText.Contains("tài liệu") ||
                lowerText.Contains("mỹ phẩm") ||
                lowerText.Contains("chất lỏng") ||
                lowerText.Contains("cây cảnh") ||
                lowerText.Contains("thú cưng"))
            {
                return await HandlePackageCategoryInquiryAsync(lowerText, userDisplayName);
            }

            // =========================================================================
            // 11. BƯU CỤC, TRẠM TRUNG CHUYỂN, ĐỊA CHỈ GỬI HÀNG
            // =========================================================================
            if (lowerText.Contains("bưu cục") ||
                lowerText.Contains("trạm") ||
                lowerText.Contains("chi nhánh") ||
                lowerText.Contains("địa chỉ") ||
                lowerText.Contains("ở đâu") ||
                lowerText.Contains("gần nhất") ||
                lowerText.Contains("bản đồ"))
            {
                return await HandleStationInquiryAsync(userDisplayName);
            }

            // =========================================================================
            // 12. CHÀO HỎI, XÃ GIAO, GIỚI THIỆU DANH TÍNH AI (SMALL TALK)
            // =========================================================================
            if (lowerText.Contains("chào") ||
                lowerText.Contains("hello") ||
                lowerText.Contains("hi") ||
                lowerText.Contains("alo") ||
                lowerText.Contains("helo") ||
                lowerText.Contains("ad ơi") ||
                lowerText.Contains("shop ơi") ||
                lowerText.Contains("bot ơi") ||
                lowerText.Contains("có ai không") ||
                lowerText.Contains("bạn là ai") ||
                lowerText.Contains("em là ai") ||
                lowerText.Contains("người thật hay") ||
                lowerText.Contains("robot"))
            {
                string greetingName = !string.IsNullOrWhiteSpace(currentUser?.FullName) ? $" anh/chị **{currentUser.FullName}**" : "";
                response.Reply = $"Dạ em xin chào{greetingName}! 👋 Em là **Trợ lý AI Tốc Độ** - trợ lý hỗ trợ trực tuyến 24/7 của hệ thống vận chuyển **Tốc Độ Delivery** ạ!\n\n" +
                                 $"Tuy là trợ lý ảo nhưng em được trang bị đầy đủ dữ liệu nghiệp vụ bưu chính để giải đáp tức thì cho anh/chị mọi thắc mắc:\n" +
                                 $"• 📦 **Tra cứu đơn hàng** & hành trình shipper theo thời gian thực\n" +
                                 $"• 💰 **Báo giá cước phí dự kiến** theo số km và cân nặng\n" +
                                 $"• 🏷️ **Tư vấn danh mục hàng hóa**, quy chuẩn đóng gói kiện hàng\n" +
                                 $"• ⏱️ **Thời gian giao hàng**, hỗ trợ giao hỏa tốc 1-3H\n" +
                                 $"• 📍 **Địa chỉ bưu cục & trạm gửi hàng** gần mình nhất\n\n" +
                                 $"Hôm nay anh/chị đang cần gửi kiện hàng gì hoặc cần em hỗ trợ kiểm tra đơn nào không ạ?";

                response.QuickPrompts = new List<string>
                {
                    "📦 Đơn hàng của tôi",
                    "💰 Tính cước 2kg 10km",
                    "⏱️ Thời gian giao hàng",
                    "📍 Bưu cục gần nhất"
                };
                return response;
            }

            // =========================================================================
            // 13. CẢM ƠN, KHEN NGỢI, TẠM BIỆT
            // =========================================================================
            if (lowerText.Contains("cảm ơn") ||
                lowerText.Contains("cam on") ||
                lowerText.Contains("thanks") ||
                lowerText.Contains("thank you") ||
                lowerText.Contains("ok em") ||
                lowerText.Contains("tốt quá") ||
                lowerText.Contains("thông minh đấy") ||
                lowerText.Contains("hay quá") ||
                lowerText.Contains("tạm biệt") ||
                lowerText.Contains("bye"))
            {
                response.Reply = $"Dạ không có chi ạ! Được hỗ trợ {userDisplayName} là niềm vinh hạnh của em. 😊✨\n\n" +
                                 $"Nếu trong quá trình gửi hàng hoặc theo dõi đơn bưu phẩm có bất kỳ điều gì cần hỗ trợ thêm, anh/chị cứ nhắn em bất cứ lúc nào nhé. Chúc {userDisplayName} một ngày tràn đầy năng lượng và niềm vui ạ! ❤️";

                response.QuickPrompts = new List<string>
                {
                    "🚚 Tạo đơn gửi mới",
                    "📦 Đơn hàng của tôi",
                    "💰 Tính cước dự kiến"
                };
                return response;
            }

            // =========================================================================
            // 14. BỘ SUY LUẬN TỰ NHIÊN CHO MỌI CÂU HỎI TỰ DO (SMART CONTEXTUAL INFERENCE)
            // TUYỆT ĐỐI KHÔNG NÓI "DẠ EM GHI NHẬN RỒI ĐỂ ĐÓ", MÀ TRẢ LỜI CỤ THỂ, TỰ NHIÊN!
            // =========================================================================
            return HandleGeneralOpenInquiry(cleanText, lowerText, userDisplayName);
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
                    Reply = $"🔍 Dạ em đã tra cứu trên toàn hệ thống nhưng không tìm thấy đơn hàng nào có mã **{orderCode}** ạ.\n\n" +
                            $"Anh/chị kiểm tra lại giúp em xem mã vận đơn có bị gõ nhầm ký tự nào không nhé (mã thường có định dạng `ORD...`). " +
                            $"Hoặc anh/chị có thể bấm xem danh sách các đơn đã đặt trong tài khoản của mình bên dưới ạ!",
                    QuickPrompts = new List<string>
                    {
                        "📦 Xem các đơn của tôi",
                        "🚚 Tạo đơn giao hàng mới",
                        "📞 Gọi tổng đài hỗ trợ 1900 8888"
                    },
                    ActionUrl = "/DeliveryOrder/Index",
                    ActionText = "Xem danh sách đơn trong tài khoản"
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
                ? $"\n• 🕒 **Tiến độ mới nhất:** {latestHistory.ChangedAt:HH:mm dd/MM/yyyy} - *{latestHistory.Status}*"
                : "";

            string shipperInfo = order.DeliveryStaff != null
                ? $"\n• 🛵 **Shipper phụ trách:** {order.DeliveryStaff.FullName} (SĐT: {order.DeliveryStaff.PhoneNumber ?? "1900 8888"})"
                : "\n• 🛵 **Shipper:** Đang điều phối tài xế gần nhất";

            string itemsText = string.Join(", ", order.OrderItems.Select(i => $"{i.Description} ({i.Quantity} cái, {i.Weight}kg)"));

            string reply = $"✅ **Dạ em đã tìm thấy thông tin đơn hàng {order.OrderCode} của mình rồi ạ:**\n\n" +
                           $"• 📌 **Trạng thái hiện tại:** **{order.CurrentStatus}**\n" +
                           $"• 👤 **Người nhận:** {order.ReceiverName} ({order.ReceiverPhone})\n" +
                           $"• 📍 **Địa chỉ giao:** {order.ReceiverAddress}\n" +
                           $"• 📦 **Kiện hàng:** {(string.IsNullOrWhiteSpace(itemsText) ? "Bưu phẩm" : itemsText)}\n" +
                           $"• ⚡ **Gói dịch vụ:** Giao {order.DeliveryMethod} ({order.DistanceKm} km)\n" +
                           $"• 💵 **Tổng cước:** {order.TotalAmount:N0} VNĐ ({order.PaymentStatus}){shipperInfo}{historyInfo}\n\n" +
                           $"Anh/chị có thể bấm vào nút theo dõi bên dưới để xem vị trí di chuyển trực tiếp trên bản đồ nhé!";

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
                ActionText = "Xem hành trình chi tiết đơn này",
                QuickPrompts = new List<string>
                {
                    "📦 Xem tất cả đơn của tôi",
                    "💰 Tính cước phí đơn khác",
                    "📍 Tìm bưu cục gần nhất"
                }
            };
        }

        private async Task<ChatResponseDto> HandleUserOrdersInquiryAsync(ApplicationUser? currentUser)
        {
            if (currentUser == null)
            {
                return new ChatResponseDto
                {
                    Reply = "🔒 Dạ để xem danh sách các đơn hàng của riêng mình, anh/chị đăng nhập tài khoản giúp em nhé!\n\n" +
                            "👉 Nếu anh/chị đang có sẵn mã vận đơn cụ thể (ví dụ: `ORD...`), anh/chị chỉ cần nhắn trực tiếp mã đơn vào đây, em tra cứu tình trạng ngay cho mình mà không cần đăng nhập ạ!",
                    ActionUrl = "/Account/Login",
                    ActionText = "Đăng nhập tài khoản ngay",
                    QuickPrompts = new List<string>
                    {
                        "💰 Tính cước phí dự kiến",
                        "🏷️ Các loại hàng hóa nhận gửi",
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
                    Reply = $"Dạ chào anh/chị **{currentUser.FullName}**! Em kiểm tra thì hiện tại tài khoản của mình chưa có đơn hàng nào được tạo trên hệ thống ạ.\n\n" +
                            "Anh/chị có muốn lên đơn giao hàng ngay bây giờ không? Shipper bên em sẽ đến tận nhà lấy hàng miễn phí chỉ sau ít phút ạ!",
                    ActionUrl = "/DeliveryOrder/Create",
                    ActionText = "Tạo đơn giao hàng mới ngay",
                    QuickPrompts = new List<string>
                    {
                        "🚚 Tạo đơn giao ngay",
                        "💰 Tính cước phí dự kiến",
                        "🏷️ Xem loại hàng nhận gửi"
                    }
                };
            }

            var orderDtos = new List<OrderSummaryDto>();
            var lines = new List<string>();

            lines.Add($"📦 **Dạ em gửi anh/chị danh sách 5 đơn hàng gần nhất của mình ({currentUser.FullName}) ạ:**\n");

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

            lines.Add("💡 *Anh/chị có thể nhắn mã đơn cụ thể (ví dụ: " + orders[0].OrderCode + ") để em kiểm tra vị trí shipper và hành trình chi tiết nhé!*");

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
                    "💰 Tính cước phí đơn khác"
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

            // Nếu người dùng cung cấp số liệu cụ thể
            if (weight > 0 || distance > 0)
            {
                if (weight <= 0) weight = 2.0m;
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
                        surchargeNote += $"\n   • 🚦 Phụ phí giờ cao điểm ({feeResult.TrafficDescription}): +{feeResult.TrafficFee:N0} đ";
                    }

                    string reply = $"💰 **Dạ em tính cước phí dự kiến cho kiện hàng của mình như sau ạ:**\n\n" +
                                   $"• 📦 **Khối lượng:** {weight:N1} kg\n" +
                                   $"• 📍 **Khoảng cách:** {distance:N1} km\n" +
                                   $"• ⚡ **Gói dịch vụ:** Giao hàng {method}\n" +
                                   $"----------------------------------\n" +
                                   $"• 💵 **Cước cự ly cơ bản:** {feeResult.BaseShippingFee:N0} đ\n" +
                                   $"• ⚖️ **Phụ phí khối lượng:** {feeResult.WeightFee:N0} đ{surchargeNote}\n" +
                                   $"----------------------------------\n" +
                                   $"👉 **TỔNG CƯỚC DỰ KIẾN:** **{feeResult.TotalPrice:N0} VNĐ**\n\n" +
                                   $"*(Mức cước đã bao gồm bảo hiểm tiêu chuẩn. Khi tạo đơn chính thức, hệ thống sẽ tự động đo quãng đường chính xác theo địa chỉ lấy và giao hàng ạ)*";

                    return new ChatResponseDto
                    {
                        Reply = reply,
                        EstimatedFee = feeResult,
                        ActionUrl = "/DeliveryOrder/Create",
                        ActionText = "Gửi đơn hàng với mức cước này",
                        QuickPrompts = new List<string>
                        {
                            "⚡ Gửi hàng hỏa tốc",
                            "💰 Tính cước 5kg 15km",
                            "🏷️ Các loại hàng nhận gửi",
                            "📦 Đơn hàng của tôi"
                        }
                    };
                }
            }

            // Nếu người dùng hỏi theo món đồ cụ thể (ví dụ: "gửi quần áo", "gửi laptop", "gửi hồ sơ")
            string recognizedItem = "";
            decimal estimatedWeight = 1.0m;
            decimal estimatedDistance = 7.0m;

            if (lowerText.Contains("quần áo") || lowerText.Contains("váy") || lowerText.Contains("giày") || lowerText.Contains("áo"))
            {
                recognizedItem = "Quần áo / Thời trang";
                estimatedWeight = 1.0m;
            }
            else if (lowerText.Contains("laptop") || lowerText.Contains("máy tính") || lowerText.Contains("điện thoại"))
            {
                recognizedItem = "Thiết bị điện tử (Laptop / Điện thoại)";
                estimatedWeight = 2.5m;
            }
            else if (lowerText.Contains("tài liệu") || lowerText.Contains("hồ sơ") || lowerText.Contains("giấy tờ") || lowerText.Contains("hợp đồng"))
            {
                recognizedItem = "Hồ sơ / Tài liệu bưu phẩm";
                estimatedWeight = 0.5m;
            }
            else if (lowerText.Contains("bánh kem") || lowerText.Contains("đồ ăn") || lowerText.Contains("hoa quả") || lowerText.Contains("trái cây"))
            {
                recognizedItem = "Thực phẩm / Bánh ngọt";
                estimatedWeight = 2.0m;
                method = "Nhanh"; // Thực phẩm ưu tiên giao nhanh
            }

            if (!string.IsNullOrWhiteSpace(recognizedItem))
            {
                var feeSample = await EstimateFeeDirectAsync(method, estimatedWeight, estimatedDistance);
                decimal price = feeSample?.TotalPrice ?? 25000;

                string reply = $"Dạ với mặt hàng **{recognizedItem}**, thông thường kiện hàng sẽ nặng khoảng **{estimatedWeight:N1}kg** ạ!\n\n" +
                               $"Nếu giao trong phạm vi nội thành (khoảng {estimatedDistance:N0}km), mức cước dự kiến sẽ vào khoảng:\n" +
                               $"👉 **Chỉ từ {price:N0} VNĐ** (Gói giao {method})\n\n" +
                               $"• Cước phí của Tốc Độ Delivery tính minh bạch theo: **Cự ly di chuyển (km)** + **Khối lượng (kg)**.\n" +
                               $"• Khi anh/chị tạo đơn, hệ thống sẽ tự động đo tọa độ chính xác giữa người gửi và người nhận để báo giá chuẩn nhất ạ.\n\n" +
                               $"Anh/chị có muốn tạo đơn ngay bây giờ hay cần em tính theo số kg và cự ly cụ thể hơn không ạ?";

                return new ChatResponseDto
                {
                    Reply = reply,
                    ActionUrl = "/DeliveryOrder/Create",
                    ActionText = "Tạo đơn giao mặt hàng này ngay",
                    QuickPrompts = new List<string>
                    {
                        "🚚 Tạo đơn giao ngay",
                        "Tính cước 2kg 10km",
                        "Tính cước 5kg 15km nhanh",
                        "🏷️ Xem quy chuẩn đóng gói"
                    }
                };
            }

            // Trường hợp hỏi giá chung chung
            return new ChatResponseDto
            {
                Reply = "💰 **Dạ bảng cước phí của Tốc Độ Delivery cực kỳ tiết kiệm và minh bạch ạ:**\n\n" +
                        "1. **Cước cự ly:** Bậc thang theo số km (rất rẻ trong nội thành).\n" +
                        "2. **Cước cân nặng:** Tùy theo khối lượng bưu kiện (hàng dưới 2kg tính mức cước cơ bản tối ưu nhất).\n" +
                        "3. **Gói dịch vụ linh hoạt:**\n" +
                        "   • 🚚 **Giao Tiêu Chuẩn:** Tiết kiệm tối đa, giao ngay trong ngày (chỉ từ 16.000đ - 22.000đ nội thành).\n" +
                        "   • ⚡ **Giao Hỏa Tốc (1-3H):** Giao thẳng tức thì không lưu kho.\n\n" +
                        "👉 Anh/chị có thể nhắn cho em khối lượng và khoảng cách (ví dụ: *\"Tính cước 2kg đi 8km\"*) hoặc cho em biết món đồ mình muốn gửi, em tính và báo mức cước chính xác ngay cho mình nhé!",
                ActionUrl = "/PriceTable/Index",
                ActionText = "Xem bảng giá chi tiết hệ thống",
                QuickPrompts = new List<string>
                {
                    "Tính cước 1kg 5km thường",
                    "Tính cước 3kg 10km nhanh",
                    "Tính cước 5kg 20km",
                    "🚚 Tạo đơn giao ngay"
                }
            };
        }

        private async Task<ChatResponseDto> HandlePackageCategoryInquiryAsync(string lowerText, string userDisplayName)
        {
            var activeTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.PackageTypeId)
                .ToListAsync();

            // Tư vấn cụ thể nếu hỏi về từng món hàng đặc biệt
            if (lowerText.Contains("đồ điện tử") || lowerText.Contains("laptop") || lowerText.Contains("điện thoại"))
            {
                return new ChatResponseDto
                {
                    Reply = $"Dạ {userDisplayName} yên tâm nhé! **Tốc Độ Delivery nhận vận chuyển đồ điện tử (Laptop, Điện thoại, Máy tính bảng, Linh kiện)** với quy trình bảo quản đặc biệt ạ:\n\n" +
                            $"• 📦 **Quy cách đóng gói:** Anh/chị bọc từ 3 - 4 lớp xốp bóng khí (bubble wrap), chèn chặt các góc và dán kín bằng băng keo niêm phong.\n" +
                            $"• 🛡️ **Bảo hiểm hàng hóa:** Bưu phẩm điện tử được bảo hiểm 100% giá trị khai báo khi giao nhận.\n" +
                            $"• 🛵 **Giao an toàn:** Shipper sẽ có túi chuyên dụng chống sốc và chụp ảnh bàn giao đối chứng lúc nhận/giao hàng.\n\n" +
                            $"Anh/chị có muốn lên đơn giao thiết bị điện tử ngay không ạ?",
                    ActionUrl = "/DeliveryOrder/Create",
                    ActionText = "Tạo đơn gửi đồ điện tử",
                    QuickPrompts = new List<string>
                    {
                        "💰 Tính cước gửi laptop",
                        "🛡️ Chính sách bảo hiểm",
                        "🚚 Tạo đơn giao hàng"
                    }
                };
            }

            if (lowerText.Contains("dễ vỡ") || lowerText.Contains("gốm sứ") || lowerText.Contains("thủy tinh") || lowerText.Contains("kính"))
            {
                return new ChatResponseDto
                {
                    Reply = $"Dạ bên em **nhận vận chuyển hàng dễ vỡ (gốm sứ, thủy tinh, mỹ phẩm lọ thủy tinh, tranh ảnh)** ạ {userDisplayName}!\n\n" +
                            $"Để đảm bảo an toàn tuyệt đối cho bưu phẩm, anh/chị lưu ý giúp em:\n" +
                            $"1. Bọc kỹ từng món bằng mút xốp hoặc xốp hơi giảm chấn 6 mặt.\n" +
                            $"2. Dán nhãn hoặc ghi rõ chữ **\"HÀNG DỄ VỠ - XIN NHẸ TAY\"** bên ngoài kiện hàng.\n" +
                            $"3. Shipper bên em được đào tạo chuyên môn sẽ cố định riêng trên xe, không chèn đè vật nặng lên trên kiện hàng của mình ạ!\n\n" +
                            $"Anh/chị cần gửi hàng dễ vỡ đi cự ly bao xa để em ước tính cước giúp mình nhé?",
                    ActionUrl = "/DeliveryOrder/Create",
                    ActionText = "Gửi hàng dễ vỡ ngay",
                    QuickPrompts = new List<string>
                    {
                        "💰 Tính cước gửi hàng dễ vỡ",
                        "⚡ Chọn gói giao Hỏa Tốc",
                        "📍 Tìm bưu cục đóng gói giúp"
                    }
                };
            }

            if (lowerText.Contains("đồ ăn") || lowerText.Contains("thực phẩm") || lowerText.Contains("bánh kem") || lowerText.Contains("trái cây"))
            {
                return new ChatResponseDto
                {
                    Reply = $"Dạ bên em nhận giao **Thực phẩm, đồ ăn đóng hộp, thực phẩm khô, trái cây tươi và bánh kem** ạ!\n\n" +
                            $"• 💡 **Khuyến nghị:** Đối với đồ ăn nóng, bánh kem hoặc thực phẩm tươi sống, anh/chị nên chọn gói **Giao Hỏa Tốc (1 - 3 Giờ)** để shipper giao thẳng trực tiếp, giữ trọn độ tươi ngon và không bị hỏng hóc.\n" +
                            $"• Không nhận gửi các loại thực phẩm bốc mùi nồng nặc chưa được bọc kín hoặc không rõ nguồn gốc xuất xứ ạ.",
                    ActionUrl = "/DeliveryOrder/Create",
                    ActionText = "Tạo đơn giao đồ ăn hỏa tốc",
                    QuickPrompts = new List<string>
                    {
                        "⚡ Tính cước hỏa tốc",
                        "🚚 Đặt shipper đến lấy đồ ăn",
                        "📦 Xem đơn hàng của tôi"
                    }
                };
            }

            if (lowerText.Contains("hàng cấm") || lowerText.Contains("thú cưng") || lowerText.Contains("chó") || lowerText.Contains("mèo"))
            {
                return new ChatResponseDto
                {
                    Reply = $"🚫 **Danh mục các mặt hàng KHÔNG nhận vận chuyển theo quy định pháp luật:**\n\n" +
                            $"• Các chất dễ cháy nổ (pháo, xăng dầu, bình gas, cồn đậm đặc).\n" +
                            $"• Vũ khí, đạn dược, chất độc hại, ma túy và các chất kích thích quốc cấm.\n" +
                            $"• Tiền mặt, vàng bạc, đá quý hoặc giấy tờ có giá trị quy đổi tiền tệ trực tiếp.\n" +
                            $"• Động vật sống (chó, mèo, gia cầm, thú cưng...).\n\n" +
                            $"Ngoài các mặt hàng trên, mọi mặt hàng thông dụng khác Tốc Độ Delivery đều nhận giao nhanh chóng và an toàn ạ!",
                    QuickPrompts = new List<string>
                    {
                        "🏷️ Các mặt hàng được nhận gửi",
                        "💰 Tính cước phí dự kiến",
                        "🚚 Tạo đơn giao ngay"
                    }
                };
            }

            // Liệt kê danh mục hàng hóa đang hoạt động trong CSDL
            var lines = new List<string>();
            lines.Add($"🏷️ **Dạ Tốc Độ Delivery nhận vận chuyển đa dạng các loại hàng hóa sau ạ {userDisplayName}:**\n");

            foreach (var type in activeTypes)
            {
                string desc = !string.IsNullOrWhiteSpace(type.Description) ? $" - {type.Description}" : "";
                lines.Add($"• **{type.TypeName}**{desc}");
            }

            lines.Add("\n🛡️ **Cam kết:** Bọc gói cẩn thận, vận chuyển an toàn và đền bù 100% nếu có sự cố.");
            lines.Add("Anh/chị đang có kiện hàng nào cần gửi không ạ? Em tư vấn gói cước phù hợp nhất cho mình nhé!");

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

        private async Task<ChatResponseDto> HandleStationInquiryAsync(string userDisplayName)
        {
            var stations = await _context.MapStations
                .Where(s => s.IsActive)
                .Take(5)
                .ToListAsync();

            var lines = new List<string>();
            lines.Add($"📍 **Dạ đây là mạng lưới các bưu cục & trạm trung chuyển của Tốc Độ Delivery phục vụ {userDisplayName} ạ:**\n");

            if (stations.Any())
            {
                foreach (var st in stations)
                {
                    lines.Add($"• 🏢 **{st.StationName}**\n  📍 Địa chỉ: {st.Address}\n");
                }
            }
            else
            {
                lines.Add("• 🏢 **Bưu cục Trung tâm Quận 1:** 123 Lê Lợi, Phường Bến Nghé, Quận 1, TP.HCM\n");
                lines.Add("• 🏢 **Trạm trung chuyển Bình Thạnh:** 45 Đinh Bộ Lĩnh, Phường 26, Bình Thạnh, TP.HCM\n");
                lines.Add("• 🏢 **Bưu cục Tân Bình:** 78 Trường Chinh, Phường 12, Tân Bình, TP.HCM\n");
            }

            lines.Add("🕒 **Giờ phục vụ:** Mở cửa liên tục 24/7 (Cả Thứ Bảy, Chủ Nhật và ngày Lễ Tết).");
            lines.Add("💡 *Nếu anh/chị bận không tiện ghé bưu cục, anh/chị cứ tạo đơn online, shipper bên em sẽ đến tận nơi lấy hàng miễn phí nhé!*");

            return new ChatResponseDto
            {
                Reply = string.Join("\n", lines),
                ActionUrl = "/Home/Stations",
                ActionText = "Xem bản đồ trạm trên toàn quốc",
                QuickPrompts = new List<string>
                {
                    "🚚 Shipper đến tận nhà lấy hàng",
                    "💰 Tính cước phí dự kiến",
                    "📦 Đơn hàng của tôi"
                }
            };
        }

        private ChatResponseDto HandleGeneralOpenInquiry(string cleanText, string lowerText, string userDisplayName)
        {
            // Trả lời thông minh theo ngữ cảnh câu hỏi tự do, không bao giờ nói máy móc "ghi nhận rồi để đó"
            string reply;

            if (lowerText.Contains("địa chỉ") || lowerText.Contains("ở đâu") || lowerText.Contains("khu vực"))
            {
                reply = $"Dạ chào {userDisplayName}! **Tốc Độ Delivery** nhận lấy và giao hàng tại khắp các quận huyện và các tỉnh thành trên toàn quốc ạ.\n\n" +
                        $"Dù anh/chị ở nội thành hay ngoại thành, shipper bên em đều có thể đến tận địa chỉ người gửi để lấy hàng và giao tận tay người nhận nhanh chóng.\n\n" +
                        $"Anh/chị đang muốn gửi hàng từ khu vực nào đến đâu ạ? Cho em biết địa chỉ em tư vấn thời gian giao và cước phí chính xác nhé!";
            }
            else if (lowerText.Contains("shipper") || lowerText.Contains("tài xế") || lowerText.Contains("giao hàng"))
            {
                reply = $"Dạ đội ngũ Shipper của **Tốc Độ Delivery** luôn túc trực 24/7 trên khắp các tuyến đường để phục vụ {userDisplayName} ạ!\n\n" +
                        $"• Shipper được trang bị app định vị GPS realtime, anh/chị hoàn toàn theo dõi được lộ trình tài xế đang đi đến đâu.\n" +
                        $"• Trước khi giao hàng, shipper luôn gọi điện thoại thông báo trước cho người nhận để hẹn giờ thuận tiện nhất.\n\n" +
                        $"Anh/chị đang cần liên hệ shipper của đơn hàng nào hay muốn đặt shipper đến lấy hàng tận nơi ạ?";
            }
            else if (lowerText.Contains("app") || lowerText.Contains("website") || lowerText.Contains("tài khoản") || lowerText.Contains("đăng ký"))
            {
                reply = $"Dạ việc đăng ký tài khoản trên **Tốc Độ Delivery** hoàn toàn miễn phí và chỉ mất chưa đầy 30 giây ạ {userDisplayName}!\n\n" +
                        $"Khi có tài khoản, anh/chị sẽ được:\n" +
                        $"• Quản lý toàn bộ danh sách đơn hàng đã gửi và nhận.\n" +
                        $"• Theo dõi hành trình bưu phẩm trực tiếp trên bản đồ.\n" +
                        $"• Đối soát tiền COD thu hộ tự động về tài khoản ngân hàng.\n\n" +
                        $"Anh/chị có thể bấm vào nút **Đăng ký** hoặc **Đăng nhập** ở góc phải phía trên màn hình để trải nghiệm ngay ạ!";
            }
            else
            {
                // Câu trả lời tự nhiên, thân thiện, mang tính giải quyết vấn đề
                reply = $"Dạ em xin phép được giải đáp thắc mắc của {userDisplayName} ạ! 😊\n\n" +
                        $"Về vấn đề anh/chị vừa hỏi: *\"{cleanText}\"*:\n" +
                        $"Hệ thống giao nhận **Tốc Độ Delivery** luôn sẵn sàng hỗ trợ linh hoạt theo nhu cầu của mình. " +
                        $"Bên em cung cấp đầy đủ các dịch vụ vận chuyển bưu phẩm, giao hỏa tốc 1-3H nội thành, giao tiêu chuẩn toàn quốc, nhận lấy hàng tận nơi và hỗ trợ thu hộ COD an toàn.\n\n" +
                        $"👉 Để em hỗ trợ {userDisplayName} được nhanh và sát nhất, anh/chị có thể cho em biết thêm chi tiết mình đang cần **gửi kiện hàng gì**, **kiểm tra đơn hàng nào** hay **ước tính cước phí** không ạ?";
            }

            return new ChatResponseDto
            {
                Reply = reply,
                QuickPrompts = new List<string>
                {
                    "📦 Đơn hàng của tôi",
                    "💰 Tính cước phí dự kiến",
                    "⏱️ Thời gian giao hàng",
                    "📍 Tìm bưu cục gần nhất",
                    "📞 Hotline CSKH 1900 8888"
                }
            };
        }
    }
}
