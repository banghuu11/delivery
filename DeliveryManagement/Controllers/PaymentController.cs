using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;

        public PaymentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPaymentService paymentService,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _paymentService = paymentService;
            _configuration = configuration;
        }

        // ==========================================
        // TRANG THANH TOÁN TRỰC TUYẾN
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Pay(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == id && o.CustomerId == user.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền thanh toán đơn này.";
                return RedirectToAction("Index", "DeliveryOrder");
            }

            // Thông tin tài khoản nhận tiền: cấu hình trong appsettings.json (mục "Payment")
            ViewBag.BankName = _configuration["Payment:BankName"] ?? "Ngân hàng (chưa cấu hình)";
            ViewBag.AccountNumber = _configuration["Payment:AccountNumber"] ?? "0000000000";
            ViewBag.AccountName = _configuration["Payment:AccountName"] ?? "TOC DO DELIVERY";

            return View(order);
        }

        // ==========================================
        // XÁC NHẬN THANH TOÁN (CỔNG THANH TOÁN MÔ PHỎNG)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(string id, bool confirm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!confirm)
            {
                TempData["ErrorMessage"] = "Vui lòng xác nhận thanh toán.";
                return RedirectToAction(nameof(Pay), new { id });
            }

            var result = await _paymentService.PayOnlineAsync(id, user.Id);

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return result.Success
                ? RedirectToAction("Details", "DeliveryOrder", new { id })
                : RedirectToAction(nameof(Pay), new { id });
        }

        // ==========================================
        // ĐỔI PHƯƠNG THỨC THANH TOÁN
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeMethod(string id, string method)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await _paymentService.ChangeMethodAsync(id, user.Id, method);

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction("Details", "DeliveryOrder", new { id });
        }
    }
}
