using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "WarehouseStaff")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WarehouseController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // DANH SÁCH ĐƠN HÀNG
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o => o.CurrentStatus != "Đã hủy")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // ==========================================
        // MỞ TRANG PHÂN LOẠI
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Classify(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.CurrentStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] =
                    "Không thể xử lý đơn hàng đã hủy.";

                return RedirectToAction(nameof(Index));
            }

            var packageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();

            var packageTypeNames = packageTypes
                .ToDictionary(
                    p => p.PackageTypeId,
                    p => p.TypeName);

            var model = new WarehouseOrderViewModel
            {
                OrderCode = order.OrderCode,
                CurrentStatus = order.CurrentStatus,
                DeliveryMethod = order.DeliveryMethod,
                CreatedAt = order.CreatedAt,

                Items = order.OrderItems
                    .OrderBy(i => i.OrderItemId)
                    .Select(i => new WarehouseItemViewModel
                    {
                        OrderItemId = i.OrderItemId,

                        OriginalPackageTypeId =
                            i.OriginalPackageTypeId
                            ?? i.PackageTypeId,

                        OriginalPackageTypeName =
                            i.OriginalPackageTypeId.HasValue &&
                            packageTypeNames.ContainsKey(
                                i.OriginalPackageTypeId.Value)
                                ? packageTypeNames[
                                    i.OriginalPackageTypeId.Value]
                                : packageTypeNames.GetValueOrDefault(
                                    i.PackageTypeId,
                                    "Chưa phân loại"),

                        PackageTypeId = i.PackageTypeId,

                        CurrentPackageTypeName =
                            packageTypeNames.GetValueOrDefault(
                                i.PackageTypeId,
                                "Chưa phân loại"),

                        Description = i.Description,
                        Size = i.Size,
                        Quantity = i.Quantity,
                        Weight = i.Weight,
                        IsFragile = i.IsFragile,
                        IsValuable = i.IsValuable,

                        ClassificationNote =
                            i.ClassificationNote
                    })
                    .ToList()
            };

            ViewBag.PackageTypes = packageTypes;

            return View(model);
        }

        // ==========================================
        // LƯU PHÂN LOẠI HÀNG HÓA
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Classify(
            WarehouseOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadPackageTypesAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == model.OrderCode);

            if (order == null)
            {
                return NotFound();
            }

            if (order.CurrentStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] =
                    "Không thể phân loại đơn hàng đã hủy.";

                return RedirectToAction(nameof(Index));
            }

            var packageTypeIds = model.Items
                .Select(i => i.PackageTypeId)
                .Distinct()
                .ToList();

            var validPackageTypeIds =
                await _context.PackageTypes
                    .Where(p =>
                        p.IsActive &&
                        packageTypeIds.Contains(p.PackageTypeId))
                    .Select(p => p.PackageTypeId)
                    .ToListAsync();

            if (validPackageTypeIds.Count != packageTypeIds.Count)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Có loại hàng hóa không hợp lệ.");

                await LoadPackageTypesAsync();

                return View(model);
            }

            // ==========================================
            // CẬP NHẬT PHÂN LOẠI
            // ==========================================

            foreach (var itemModel in model.Items)
            {
                var item = order.OrderItems
                    .FirstOrDefault(i =>
                        i.OrderItemId == itemModel.OrderItemId);

                if (item == null)
                {
                    continue;
                }

                // Nếu đơn cũ chưa có OriginalPackageTypeId
                // thì lưu lại loại ban đầu trước khi đổi.
                if (!item.OriginalPackageTypeId.HasValue)
                {
                    item.OriginalPackageTypeId =
                        item.PackageTypeId;
                }

                item.PackageTypeId =
                    itemModel.PackageTypeId;

                item.ClassifiedBy =
                    user.Id;

                item.ClassifiedAt =
                    DateTime.Now;

                item.ClassificationNote =
                    itemModel.ClassificationNote;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã lưu phân loại hàng hóa cho đơn {order.OrderCode}.";

            return RedirectToAction(
                nameof(Classify),
                new { id = order.OrderCode });
        }

        // ==========================================
        // CẬP NHẬT TRẠNG THÁI
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            string id,
            string newStatus)
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

            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng.";

                return RedirectToAction(nameof(Index));
            }

            if (order.CurrentStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] =
                    "Không thể cập nhật đơn hàng đã hủy.";

                return RedirectToAction(
                    nameof(Classify),
                    new { id = order.OrderCode });
            }

            // ==========================================
            // TRẠNG THÁI ĐƯỢC PHÉP
            // ==========================================

            var validStatuses = new[]
            {
                "Chưa nhận",
                "Đã nhận - Chưa giao",
                "Đã nhận – Đang giao",
                "Đã Giao"
            };

            if (!validStatuses.Contains(newStatus))
            {
                TempData["ErrorMessage"] =
                    "Trạng thái không hợp lệ.";

                return RedirectToAction(
                    nameof(Classify),
                    new { id = order.OrderCode });
            }

            // ==========================================
            // KIỂM TRA CHUYỂN TRẠNG THÁI
            // ==========================================

            bool validTransition =
                (order.CurrentStatus == "Chưa nhận" &&
                 newStatus == "Đã nhận - Chưa giao")
                ||
                (order.CurrentStatus == "Đã nhận - Chưa giao" &&
                 newStatus == "Đã nhận – Đang giao")
                ||
                (order.CurrentStatus == "Đã nhận – Đang giao" &&
                 newStatus == "Đã Giao");

            if (!validTransition)
            {
                TempData["ErrorMessage"] =
                    $"Không thể chuyển từ \"{order.CurrentStatus}\" sang \"{newStatus}\".";

                return RedirectToAction(
                    nameof(Classify),
                    new { id = order.OrderCode });
            }

            order.CurrentStatus = newStatus;

            var history = new OrderStatusHistory
            {
                OrderCode = order.OrderCode,
                Status = newStatus,
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            };

            _context.OrderStatusHistories.Add(history);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã cập nhật trạng thái đơn {order.OrderCode} thành \"{newStatus}\".";

            return RedirectToAction(
                nameof(Classify),
                new { id = order.OrderCode });
        }

        // ==========================================
        // LOAD PACKAGE TYPES
        // ==========================================

        private async Task LoadPackageTypesAsync()
        {
            ViewBag.PackageTypes =
                await _context.PackageTypes
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.TypeName)
                    .ToListAsync();
        }
    }
}