using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    public class ChatMessageRequest
    {
        public string Message { get; set; } = string.Empty;
        public int? ConversationId { get; set; }
    }

    public class QuickEstimateRequest
    {
        public string DeliveryMethod { get; set; } = "Thường";
        public decimal Weight { get; set; } = 1.0m;
        public decimal Distance { get; set; } = 5.0m;
    }

    public class ChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ChatController(
            IAiChatService aiChatService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _aiChatService = aiChatService;
            _userManager = userManager;
            _context = context;
        }

        // ==========================================
        // 1. GIAO DIỆN CHAT TRANG ĐẦY ĐỦ (FULL PAGE)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(int? conversationId)
        {
            string? userId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
            var conv = await _aiChatService.GetOrCreateConversationAsync(userId, conversationId);

            ViewBag.ConversationId = conv?.ConversationId ?? 0;
            ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated == true;
            ViewBag.UserName = User.Identity?.Name ?? "Khách hàng";

            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "Khách hàng";
            }
            else
            {
                ViewBag.UserFullName = "Khách vãng lai";
            }

            return View();
        }

        // ==========================================
        // 2. API GỬI TIN NHẮN TỚI AI (AJAX POST)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { success = false, message = "Nội dung tin nhắn không được để trống." });
            }

            string? userId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
            var result = await _aiChatService.ProcessMessageAsync(request.Message, userId, request.ConversationId);

            return Json(result);
        }

        // ==========================================
        // 3. API LẤY LỊCH SỬ TIN NHẮN (AJAX GET)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> History(int? conversationId)
        {
            string? userId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
            var conv = await _aiChatService.GetOrCreateConversationAsync(userId, conversationId);

            if (conv == null)
            {
                return Json(new { success = false, message = "Không tìm thấy cuộc trò chuyện." });
            }

            var messages = await _aiChatService.GetConversationHistoryAsync(conv.ConversationId, userId);
            string userFullName = "Khách hàng";
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                userFullName = user?.FullName ?? user?.UserName ?? "Khách hàng";
            }

            return Json(new
            {
                success = true,
                conversationId = conv.ConversationId,
                userFullName,
                messages
            });
        }

        // ==========================================
        // 4. API TÍNH CƯỚC NHANH TRONG WIDGET (AJAX POST)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EstimateFee([FromBody] QuickEstimateRequest model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var result = await _aiChatService.EstimateFeeDirectAsync(model.DeliveryMethod, model.Weight, model.Distance);
            if (result == null)
            {
                return Json(new { success = false, message = "Không thể tính cước cho thông số này." });
            }

            return Json(new { success = true, data = result });
        }
    }
}
