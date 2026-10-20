
using System.IO;
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
                    o.CurrentStatus == "Đã nhận - Chưa giao" &&
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
                TempData["ErrorMessage"] =
                    "Không tìm thấy mã đơn hàng.";

                return RedirectToAction(nameof(Index));
            }

            // Cập nhật có điều kiện để chỉ một shipper
            // có thể nhận thành công cùng một đơn.
            var affectedRows = await _context.DeliveryOrders
                .Where(o =>
                    o.OrderCode == id &&
                    o.DeliveryMethod == "Thường" &&
                    o.CurrentStatus == "Đã nhận - Chưa giao" &&
                    o.DeliveryStaffId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        o => o.DeliveryStaffId,
                        user.Id));

            if (affectedRows == 0)
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

            var affectedRows = await _context.DeliveryOrders
                .Where(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == "Đã nhận - Chưa giao")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        o => o.CurrentStatus,
                        "Đã nhận – Đang giao"));

            if (affectedRows == 0)
            {
                TempData["ErrorMessage"] =
                    "Không thể bắt đầu giao. Hãy kiểm tra đơn hàng của bạn.";

                return RedirectToAction(nameof(Index));
            }

            _context.OrderStatusHistories.Add(
                new OrderStatusHistory
                {
                    OrderCode = id,
                    Status = "Đã nhận – Đang giao",
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đơn {id} đã chuyển sang trạng thái đang giao.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // XÁC NHẬN GIAO HÀNG KÈM ẢNH MINH CHỨNG
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // Chỉ shipper đang phụ trách và đang giao
            // mới được xác nhận kết quả giao hàng.
            var orderExists = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == "Đã nhận – Đang giao");

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

            const long maxFileSize = 2 * 1024 * 1024;

            if (proofImage.Length > maxFileSize)
            {
                TempData["ErrorMessage"] =
                    "Ảnh minh chứng không được vượt quá 2 MB.";

                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(proofImage.FileName);

            if ((!extension.Equals(
                    ".jpg",
                    StringComparison.OrdinalIgnoreCase) &&
                 !extension.Equals(
                    ".jpeg",
                    StringComparison.OrdinalIgnoreCase)) ||
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

            var uploadsFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "delivery-proofs");

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var physicalPath = Path.Combine(
                uploadsFolder,
                fileName);

            var relativePath =
                $"/uploads/delivery-proofs/{fileName}";

            await using (var output = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write))
            {
                await proofImage.CopyToAsync(output);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Kiểm tra lại trạng thái lúc cập nhật,
                // tránh xác nhận hai lần cùng một đơn.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == "Đã nhận – Đang giao")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            o => o.CurrentStatus,
                            "Đã Giao")
                        .SetProperty(
                            o => o.DeliveryResult,
                            "Giao thành công")
                        .SetProperty(
                            o => o.ProofImage,
                            relativePath)
                        .SetProperty(
                            o => o.DeliveryNote,
                            deliveryNote));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    System.IO.File.Delete(physicalPath);

                    TempData["ErrorMessage"] =
                        "Không thể xác nhận. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
                    {
                        OrderCode = id,
                        Status = "Đã Giao",
                        ChangedBy = user.Id,
                        ChangedAt = DateTime.Now
                    });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

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
    }
}
