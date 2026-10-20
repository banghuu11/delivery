
using System;
using System.Linq;
using System.Threading.Tasks;
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
        private const string ReadyStatus = "Đã nhận - Chưa giao";
        private const string FailedStatus = "Giao thất bại - Chờ xử lý";
        private const string ReturnStatus = "Chờ hoàn hàng";

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
        // ĐIỀU PHỐI ĐƠN GIAO NHANH (US03)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Dispatch()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.DeliveryMethod == "Nhanh" &&
                    o.CurrentStatus == ReadyStatus &&
                    o.DeliveryStaffId == null)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.DeliveryStaffs =
                await _userManager.GetUsersInRoleAsync("DeliveryStaff");

            return View(orders);
        }

        // ==========================================
        // PHÂN CÔNG ĐƠN GIAO NHANH
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignExpressOrder(
            string id,
            string deliveryStaffId)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(deliveryStaffId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng chọn đơn hàng và nhân viên giao hàng.";

                return RedirectToAction(nameof(Dispatch));
            }

            var staff = await _userManager.FindByIdAsync(deliveryStaffId);

            if (staff == null ||
                !await _userManager.IsInRoleAsync(staff, "DeliveryStaff"))
            {
                TempData["ErrorMessage"] =
                    "Nhân viên giao hàng không hợp lệ.";

                return RedirectToAction(nameof(Dispatch));
            }

            var affectedRows = await _context.DeliveryOrders
                .Where(o =>
                    o.OrderCode == id &&
                    o.DeliveryMethod == "Nhanh" &&
                    o.CurrentStatus == ReadyStatus &&
                    o.DeliveryStaffId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.DeliveryStaffId, deliveryStaffId));

            if (affectedRows != 1)
            {
                TempData["ErrorMessage"] =
                    "Không thể phân công. Đơn hàng không còn khả dụng " +
                    "hoặc đã có nhân viên phụ trách.";

                return RedirectToAction(nameof(Dispatch));
            }

            TempData["SuccessMessage"] =
                $"Đã phân công đơn {id} cho nhân viên {staff.FullName}.";

            return RedirectToAction(nameof(Dispatch));
        }

        // ==========================================
        // DANH SÁCH ĐƠN GIAO THẤT BẠI (US03 + US07)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> FailedDeliveries()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o => o.CurrentStatus == FailedStatus)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // ==========================================
        // XỬ LÝ GIAO LẠI ĐƠN THẤT BẠI
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryFailedDelivery(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(FailedDeliveries));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Chỉ xử lý nếu đơn vẫn đang chờ xử lý giao thất bại.
                // Xóa người phụ trách cũ để phân công/nhận lại theo hình thức.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.CurrentStatus == FailedStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.CurrentStatus, ReadyStatus)
                        .SetProperty(o => o.DeliveryStaffId, (string?)null));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể giao lại. Đơn có thể đã được người khác xử lý.";

                    return RedirectToAction(nameof(FailedDeliveries));
                }

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderCode = id,
                    Status = ReadyStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đơn {id} đã được đưa về trạng thái chờ giao lại. " +
                    "Đơn giao nhanh sẽ chờ kho phân công; đơn giao thường sẽ xuất hiện trong danh sách chờ nhận.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Có lỗi khi xử lý giao lại đơn hàng.";
            }

            return RedirectToAction(nameof(FailedDeliveries));
        }

        // ==========================================
        // CHUYỂN ĐƠN SANG CHỜ HOÀN HÀNG
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReturn(
            string id,
            string? resolutionNote)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(FailedDeliveries));
            }

            resolutionNote = resolutionNote?.Trim();

            if (resolutionNote?.Length > 500)
            {
                TempData["ErrorMessage"] =
                    "Ghi chú xử lý không được vượt quá 500 ký tự.";

                return RedirectToAction(nameof(FailedDeliveries));
            }

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.CurrentStatus == FailedStatus);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Đơn không còn ở trạng thái chờ xử lý giao thất bại.";

                return RedirectToAction(nameof(FailedDeliveries));
            }

            var oldNote = order.DeliveryNote?.Trim();
            var returnNote = "[Xử lý kho: Chuyển sang chờ hoàn hàng]";

            if (!string.IsNullOrWhiteSpace(resolutionNote))
            {
                returnNote += $" {resolutionNote}";
            }

            var newNote = string.IsNullOrWhiteSpace(oldNote)
                ? returnNote
                : $"{oldNote}{Environment.NewLine}{returnNote}";

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.CurrentStatus == FailedStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.CurrentStatus, ReturnStatus)
                        .SetProperty(
                            o => o.DeliveryResult,
                            "Giao thất bại - Chờ hoàn hàng")
                        .SetProperty(o => o.DeliveryNote, newNote));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể chuyển hoàn hàng. Trạng thái đơn đã thay đổi.";

                    return RedirectToAction(nameof(FailedDeliveries));
                }

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderCode = id,
                    Status = ReturnStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đơn {id} đã chuyển sang trạng thái chờ hoàn hàng.";
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Có lỗi khi chuyển đơn sang chờ hoàn hàng.";
            }

            return RedirectToAction(nameof(FailedDeliveries));
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
                .ToDictionary(p => p.PackageTypeId, p => p.TypeName);

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
                            i.OriginalPackageTypeId ?? i.PackageTypeId,

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
                        Quantity = i.Quantity,
                        Weight = i.Weight,
                        IsFragile = i.IsFragile,
                        IsValuable = i.IsValuable,
                        ClassificationNote = i.ClassificationNote
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
                .FirstOrDefaultAsync(o => o.OrderCode == model.OrderCode);

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

            var validPackageTypeIds = await _context.PackageTypes
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

            foreach (var itemModel in model.Items)
            {
                var item = order.OrderItems
                    .FirstOrDefault(i =>
                        i.OrderItemId == itemModel.OrderItemId);

                if (item == null)
                {
                    continue;
                }

                if (!item.OriginalPackageTypeId.HasValue)
                {
                    item.OriginalPackageTypeId = item.PackageTypeId;
                }

                item.PackageTypeId = itemModel.PackageTypeId;
                item.ClassifiedBy = user.Id;
                item.ClassifiedAt = DateTime.Now;
                item.ClassificationNote = itemModel.ClassificationNote;
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
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
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

            // Kho chỉ đưa đơn từ Chưa nhận sang sẵn sàng giao.
            bool validTransition =
                order.CurrentStatus == "Chưa nhận" &&
                newStatus == ReadyStatus;

            if (!validTransition)
            {
                TempData["ErrorMessage"] =
                    "Nhân viên kho chỉ được chuyển đơn từ " +
                    "\"Chưa nhận\" sang \"Đã nhận - Chưa giao\". " +
                    "Các trạng thái giao hàng do nhân viên giao hàng cập nhật.";

                return RedirectToAction(
                    nameof(Classify),
                    new { id = order.OrderCode });
            }

            order.CurrentStatus = newStatus;

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderCode = order.OrderCode,
                Status = newStatus,
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            });

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
            ViewBag.PackageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();
        }
    }
}
