using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Services;
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
        private const string ReadyStatus = "Đã nhận - Chưa giao";
        private const string DeliveringStatus = "Đã nhận – Đang giao";
        private const string DeliveredStatus = "Đã Giao";
        private const string FailedStatus = "Giao thất bại - Chờ xử lý";
        private const string CanceledStatus = "Đã hủy";

        private const long MaxProofSize = 2 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IPaymentService _paymentService;

        public DeliveryStaffController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IPaymentService paymentService)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _paymentService = paymentService;
        }

        // ==========================================
        // DANH SÁCH ĐƠN ĐƯỢC PHÂN CÔNG CHO SHIPPER
        // Chỉ hiển thị các đơn được gán cho tài khoản hiện tại.
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var myOrders = await _context.DeliveryOrders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus != CanceledStatus)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // Truyền danh sách trực tiếp cho View.
            // View có thể dùng Model hoặc ViewBag.MyOrders.
            ViewBag.MyOrders = myOrders;

            return View(myOrders);
        }

        // ==========================================
        // CHI TIẾT ĐƠN GIAO HÀNG
        // Chỉ shipper được phân công mới được xem.
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id);

            // Không cho shipper xem đơn được phân công cho người khác.
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // ==========================================
        // ACTION CŨ: SHIPPER TỰ NHẬN ĐƠN
        // Không cho phép tự nhận; kho phải phân công.
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AcceptOrder(string id)
        {
            TempData["ErrorMessage"] =
                "Shipper không được tự nhận đơn. Vui lòng chờ kho phân công đơn hàng.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // ACTION CŨ: BẮT ĐẦU GIAO HÀNG
        // Kho đã chuyển trạng thái khi phân công.
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
                TempData["ErrorMessage"] =
                    "Không tìm thấy mã đơn hàng.";

                return RedirectToAction(nameof(Index));
            }

            var orderExists = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (!orderExists)
            {
                TempData["ErrorMessage"] =
                    "Đơn không thuộc quyền xử lý của bạn hoặc chưa được kho phân công.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"Đơn {id} đã ở trạng thái đang giao. Bạn có thể cập nhật kết quả giao hàng.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // XÁC NHẬN GIAO HÀNG THÀNH CÔNG KÈM ẢNH
        // Chỉ shipper được phân công và đơn đang giao mới được xác nhận.
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
                TempData["ErrorMessage"] =
                    "Không tìm thấy mã đơn hàng.";

                return RedirectToAction(nameof(Index));
            }

            var orderExists = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (!orderExists)
            {
                TempData["ErrorMessage"] =
                    "Đơn không tồn tại, không thuộc quyền xử lý của bạn hoặc không ở trạng thái đang giao.";

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

            if ((!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                 !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) ||
                !string.Equals(
                    proofImage.ContentType,
                    "image/jpeg",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Chỉ chấp nhận ảnh JPG/JPEG.";

                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra chữ ký JPEG cơ bản trước khi lưu file.
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
                    _environment.ContentRootPath,
                    "wwwroot");
            }

            var uploadsFolder = Path.Combine(
                webRoot,
                "uploads",
                "delivery-proofs");

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var physicalPath = Path.Combine(uploadsFolder, fileName);
            var relativePath = $"/uploads/delivery-proofs/{fileName}";

            try
            {
                // Lưu ảnh minh chứng.
                await using (var output = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await proofImage.CopyToAsync(output);
                }

                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                // Cập nhật có điều kiện lần cuối:
                // phải đúng shipper và đơn vẫn đang giao.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == DeliveringStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            o => o.CurrentStatus,
                            DeliveredStatus)
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

                    TryDeleteFile(physicalPath);

                    TempData["ErrorMessage"] =
                        "Không thể xác nhận. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
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
                TryDeleteFile(physicalPath);

                TempData["ErrorMessage"] =
                    "Có lỗi khi lưu kết quả giao hàng. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            // Thu tiền COD sau khi kết quả giao thành công đã được lưu.
            await _paymentService.CollectCodAsync(id);

            TempData["SuccessMessage"] =
                $"Đã ghi nhận giao hàng thành công cho đơn {id}.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // GHI NHẬN GIAO HÀNG THẤT BẠI
        // Sau khi thất bại, kho xử lý giao lại hoặc hoàn hàng.
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
                TempData["ErrorMessage"] =
                    "Không tìm thấy mã đơn hàng.";

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

            var failureDetail =
                $"Lý do giao thất bại: {failureReason}";

            if (!string.IsNullOrWhiteSpace(deliveryNote))
            {
                failureDetail += $". Ghi chú: {deliveryNote}";
            }

            var previousNote = order.DeliveryNote?.Trim();

            var noteToSave = string.IsNullOrWhiteSpace(previousNote)
                ? failureDetail
                : $"{previousNote}{Environment.NewLine}{failureDetail}";

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == DeliveringStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            o => o.CurrentStatus,
                            FailedStatus)
                        .SetProperty(
                            o => o.DeliveryResult,
                            "Giao thất bại")
                        .SetProperty(
                            o => o.DeliveryNote,
                            noteToSave));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể cập nhật. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
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
                $"Đã ghi nhận giao thất bại cho đơn {order.OrderCode}. Đơn đang chờ kho xử lý tiếp.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // XÓA FILE MINH CHỨNG NẾU CẬP NHẬT CSDL THẤT BẠI
        // ==========================================

        private static void TryDeleteFile(string physicalPath)
        {
            try
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch
            {
                // Giữ nguyên lỗi chính nếu xóa file thất bại.
            }
        }
    }
}