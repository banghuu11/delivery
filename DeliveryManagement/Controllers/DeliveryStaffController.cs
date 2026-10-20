
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "DeliveryStaff")]
    public class DeliveryStaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        private const string ReadyStatus = "Đã nhận - Chưa giao";
        private const string DeliveringStatus = "Đã nhận – Đang giao";
        private const string DeliveredStatus = "Đã Giao";
        private const string FailedStatus = "Giao thất bại - Chờ xử lý";
        private const long MaxProofSize = 2 * 1024 * 1024;

        public DeliveryStaffController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // ==========================================
        // DANH SÁCH ĐƠN CHỜ NHẬN VÀ ĐƠN CỦA TÔI
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            // Chỉ đơn giao thường chưa có người phụ trách
            // mới được shipper tự nhận.
            var availableOrders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.DeliveryMethod == "Thường" &&
                    o.CurrentStatus == ReadyStatus &&
                    o.DeliveryStaffId == null)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            // Hiển thị các đơn được giao cho tài khoản hiện tại.
            var myOrders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus != "Đã hủy")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.MyOrders = myOrders;

            return View(availableOrders);
        }

        // ==========================================
        // SHIPPER TỰ NHẬN ĐƠN GIAO THƯỜNG
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật có điều kiện để chỉ một shipper
            // có thể nhận thành công cùng một đơn.
            var affectedRows = await _context.DeliveryOrders
                .Where(o =>
                    o.OrderCode == id &&
                    o.DeliveryMethod == "Thường" &&
                    o.CurrentStatus == ReadyStatus &&
                    o.DeliveryStaffId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.DeliveryStaffId, user.Id));

            if (affectedRows != 1)
            {
                TempData["ErrorMessage"] =
                    "Đơn này không còn khả dụng hoặc đã được shipper khác nhận.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"Bạn đã nhận đơn {id} thành công.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // BẮT ĐẦU GIAO HÀNG
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartDelivery(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var affectedRows = await _context.DeliveryOrders
                .Where(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == ReadyStatus)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.CurrentStatus, DeliveringStatus));

            if (affectedRows != 1)
            {
                TempData["ErrorMessage"] =
                    "Không thể bắt đầu giao. Hãy kiểm tra đơn hàng của bạn.";

                return RedirectToAction(nameof(Index));
            }

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderCode = id,
                Status = DeliveringStatus,
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đơn {id} đã chuyển sang trạng thái đang giao.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // XÁC NHẬN GIAO HÀNG THÀNH CÔNG KÈM ẢNH
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<IActionResult> CompleteDelivery(
            string id,
            IFormFile? proofImage,
            string? deliveryNote)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Chỉ shipper đang phụ trách và đang giao
            // mới được xác nhận kết quả giao hàng.
            var orderExists = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (!orderExists)
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng không tồn tại hoặc không thuộc quyền xử lý của bạn.";

                return RedirectToAction(nameof(Index));
            }

            // Bắt buộc có ảnh minh chứng.
            if (proofImage == null || proofImage.Length == 0)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng tải ảnh minh chứng giao hàng.";

                return RedirectToAction(nameof(Index));
            }

            if (proofImage.Length > MaxProofSize)
            {
                TempData["ErrorMessage"] =
                    "Ảnh minh chứng không được vượt quá 2 MB.";

                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(proofImage.FileName);

            if ((!extension.Equals(
                    ".jpg", StringComparison.OrdinalIgnoreCase) &&
                 !extension.Equals(
                    ".jpeg", StringComparison.OrdinalIgnoreCase)) ||
                !string.Equals(
                    proofImage.ContentType,
                    "image/jpeg",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Chỉ chấp nhận ảnh JPG/JPEG.";

                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra chữ ký JPEG cơ bản của file.
            await using (var input = proofImage.OpenReadStream())
            {
                var signature = new byte[3];
                var bytesRead = await input.ReadAsync(
                    signature, 0, signature.Length);

                if (bytesRead != 3 ||
                    signature[0] != 0xFF ||
                    signature[1] != 0xD8 ||
                    signature[2] != 0xFF)
                {
                    TempData["ErrorMessage"] =
                        "File tải lên không phải ảnh JPEG hợp lệ.";

                    return RedirectToAction(nameof(Index));
                }
            }

            deliveryNote = deliveryNote?.Trim();

            if (deliveryNote?.Length > 500)
            {
                TempData["ErrorMessage"] =
                    "Ghi chú không được vượt quá 500 ký tự.";

                return RedirectToAction(nameof(Index));
            }

            var webRoot = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    _environment.ContentRootPath, "wwwroot");
            }

            var uploadsFolder = Path.Combine(
                webRoot, "uploads", "delivery-proofs");

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var physicalPath = Path.Combine(uploadsFolder, fileName);
            var relativePath = $"/uploads/delivery-proofs/{fileName}";

            await using (var output = new FileStream(
                physicalPath, FileMode.CreateNew, FileAccess.Write))
            {
                await proofImage.CopyToAsync(output);
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == DeliveringStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.CurrentStatus, DeliveredStatus)
                        .SetProperty(o => o.DeliveryResult, "Giao thành công")
                        .SetProperty(o => o.ProofImage, relativePath)
                        .SetProperty(o => o.DeliveryNote, deliveryNote));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể xác nhận. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderCode = id,
                    Status = DeliveredStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }

                TempData["ErrorMessage"] =
                    "Có lỗi khi lưu kết quả giao hàng. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"Đã ghi nhận giao hàng thành công cho đơn {id}.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // GHI NHẬN GIAO HÀNG THẤT BẠI
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FailDelivery(
            string id,
            string failureReason,
            string? deliveryNote)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var validReasons = new[]
            {
                "Không liên lạc được với người nhận",
                "Sai địa chỉ",
                "Khách từ chối nhận",
                "Lý do khác"
            };

            if (!validReasons.Contains(failureReason))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng chọn lý do giao thất bại hợp lệ.";

                return RedirectToAction(nameof(Index));
            }

            deliveryNote = deliveryNote?.Trim();

            if (deliveryNote?.Length > 500)
            {
                TempData["ErrorMessage"] =
                    "Ghi chú không được vượt quá 500 ký tự.";

                return RedirectToAction(nameof(Index));
            }

            if (failureReason == "Lý do khác" &&
                string.IsNullOrWhiteSpace(deliveryNote))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng nhập ghi chú khi chọn lý do khác.";

                return RedirectToAction(nameof(Index));
            }

            // Chỉ cho phép shipper được phân công xử lý đơn đang giao.
            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Đơn không tồn tại, không thuộc quyền xử lý của bạn hoặc không ở trạng thái đang giao.";

                return RedirectToAction(nameof(Index));
            }

            var failureDetail = $"Lý do giao thất bại: {failureReason}";

            if (!string.IsNullOrWhiteSpace(deliveryNote))
            {
                failureDetail += $". Ghi chú: {deliveryNote}";
            }

            // Giữ lại ghi chú cũ (nếu có) rồi nối thêm lý do thất bại.
            var previousNote = order.DeliveryNote?.Trim();

            var noteToSave = string.IsNullOrWhiteSpace(previousNote)
                ? failureDetail
                : $"{previousNote}{Environment.NewLine}{failureDetail}";

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Cập nhật có điều kiện để tránh xử lý đơn hai lần.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == DeliveringStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.CurrentStatus, FailedStatus)
                        .SetProperty(o => o.DeliveryResult, "Giao thất bại")
                        .SetProperty(o => o.DeliveryNote, noteToSave));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể cập nhật. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderCode = order.OrderCode,
                    Status = FailedStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Có lỗi khi lưu kết quả giao thất bại. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"Đã ghi nhận giao thất bại cho đơn {order.OrderCode}. Đơn đang chờ xử lý tiếp.";

            return RedirectToAction(nameof(Index));
        }
    }
}
