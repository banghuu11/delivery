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
        private const string NotReceivedStatus = "Chưa nhận";
        private const string ReadyStatus = "Đã nhận - Chưa giao";
        private const string InTransitStatus = "Đã nhận – Đang giao";
        private const string FailedStatus = "Giao thất bại - Chờ xử lý";
        private const string DeliveredStatus = "Đã Giao";
        private const string CanceledStatus = "Đã hủy";
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
        // DANH SÁCH ĐƠN HÀNG TRONG KHO
        // Chỉ hiển thị đơn đã được ReceptionStaff ghi nhận.
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.ReceivedAt.HasValue &&
                    o.CurrentStatus != NotReceivedStatus &&
                    o.CurrentStatus != CanceledStatus)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // ==========================================
        // DANH SÁCH ĐƠN ĐÃ TIẾP NHẬN, CHỜ PHÂN CÔNG
        // Chỉ đơn đã ghi nhận, ở trạng thái Ready và chưa có shipper.
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Dispatch()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.ReceivedAt.HasValue &&
                    o.CurrentStatus == ReadyStatus &&
                    o.DeliveryStaffId == null)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.DeliveryStaffs =
                await _userManager.GetUsersInRoleAsync("DeliveryStaff");

            return View(orders);
        }

        // ==========================================
        // PHÂN CÔNG SHIPPER VÀ CHUYỂN SANG ĐANG GIAO
        // Chỉ phân công đơn đã được ReceptionStaff ghi nhận.
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

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Điều kiện ReceivedAt.HasValue giúp chặn đơn chưa được tiếp nhận,
                // kể cả khi CurrentStatus bị sửa sai hoặc dữ liệu không đồng nhất.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.ReceivedAt.HasValue &&
                        o.CurrentStatus == ReadyStatus &&
                        o.DeliveryStaffId == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.DeliveryStaffId, deliveryStaffId)
                        .SetProperty(o => o.CurrentStatus, InTransitStatus));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Không thể phân công. Đơn chưa được tiếp nhận, " +
                        "đã được phân công hoặc trạng thái đã thay đổi.";
                    return RedirectToAction(nameof(Dispatch));
                }

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderCode = id,
                    Status = InTransitStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Đã phân công đơn {id} cho nhân viên {staff.FullName}. " +
                    $"Trạng thái đơn hàng: {InTransitStatus}.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi phân công đơn hàng.";
            }

            return RedirectToAction(nameof(Dispatch));
        }

        // ==========================================
        // DANH SÁCH ĐƠN GIAO THẤT BẠI
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> FailedDeliveries()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.ReceivedAt.HasValue &&
                    o.CurrentStatus == FailedStatus)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // ==========================================
        // GIAO LẠI ĐƠN THẤT BẠI
        // Giao thất bại - Chờ xử lý -> Đã nhận - Chưa giao
        // Xóa shipper cũ để kho phân công lại.
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
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.ReceivedAt.HasValue &&
                        o.CurrentStatus == FailedStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.CurrentStatus, ReadyStatus)
                        .SetProperty(o => o.DeliveryStaffId, (string?)null)
                        .SetProperty(o => o.DeliveryResult, (string?)null));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Không thể giao lại. Đơn có thể đã được xử lý " +
                        "hoặc trạng thái đã thay đổi.";
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
                    $"Đơn {id} đã chuyển về trạng thái \"{ReadyStatus}\". " +
                    "Kho có thể phân công lại nhân viên giao hàng.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi xử lý giao lại đơn hàng.";
            }

            return RedirectToAction(nameof(FailedDeliveries));
        }

        // ==========================================
        // CHUYỂN ĐƠN THẤT BẠI SANG CHỜ HOÀN HÀNG
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
                    o.ReceivedAt.HasValue &&
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
                        o.ReceivedAt.HasValue &&
                        o.CurrentStatus == FailedStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.CurrentStatus, ReturnStatus)
                        .SetProperty(o => o.DeliveryResult, "Giao thất bại - Chờ hoàn hàng")
                        .SetProperty(o => o.DeliveryNote, newNote)
                        .SetProperty(o => o.DeliveryStaffId, (string?)null));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] =
                        "Không thể chuyển sang chờ hoàn hàng. " +
                        "Trạng thái đơn có thể đã thay đổi.";
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
                    $"Đơn {id} đã chuyển sang trạng thái \"{ReturnStatus}\".";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi chuyển đơn sang chờ hoàn hàng.";
            }

            return RedirectToAction(nameof(FailedDeliveries));
        }

        // ==========================================
        // MỞ TRANG PHÂN LOẠI HÀNG HÓA
        // Chỉ cho phép phân loại đơn đã được tiếp nhận và đang chờ phân công.
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

            if (!order.ReceivedAt.HasValue ||
                order.CurrentStatus != ReadyStatus)
            {
                TempData["ErrorMessage"] =
                    "Chỉ được phân loại đơn đã được ReceptionStaff ghi nhận " +
                    "và đang ở trạng thái \"Đã nhận - Chưa giao\".";
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
                            packageTypeNames.ContainsKey(i.OriginalPackageTypeId.Value)
                                ? packageTypeNames[i.OriginalPackageTypeId.Value]
                                : packageTypeNames.GetValueOrDefault(
                                    i.PackageTypeId, "Chưa phân loại"),
                        PackageTypeId = i.PackageTypeId,
                        CurrentPackageTypeName = packageTypeNames.GetValueOrDefault(
                            i.PackageTypeId, "Chưa phân loại"),
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
        // Chặn cả GET và POST để đơn chưa tiếp nhận không bị chỉnh trực tiếp.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Classify(WarehouseOrderViewModel model)
        {
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

            if (!order.ReceivedAt.HasValue ||
                order.CurrentStatus != ReadyStatus)
            {
                TempData["ErrorMessage"] =
                    "Không thể phân loại. Đơn chưa được tiếp nhận hoặc " +
                    "đã chuyển sang bước xử lý khác.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                await LoadPackageTypesAsync();
                return View(model);
            }

            model.Items ??= new System.Collections.Generic.List<WarehouseItemViewModel>();

            var packageTypeIds = model.Items
                .Select(i => i.PackageTypeId)
                .Distinct()
                .ToList();

            var validPackageTypeIds = await _context.PackageTypes
                .Where(p => p.IsActive && packageTypeIds.Contains(p.PackageTypeId))
                .Select(p => p.PackageTypeId)
                .ToListAsync();

            if (validPackageTypeIds.Count != packageTypeIds.Count)
            {
                ModelState.AddModelError(string.Empty, "Có loại hàng hóa không hợp lệ.");
                await LoadPackageTypesAsync();
                return View(model);
            }

            foreach (var itemModel in model.Items)
            {
                var item = order.OrderItems.FirstOrDefault(i =>
                    i.OrderItemId == itemModel.OrderItemId);

                // Không cho cập nhật những dòng không thuộc đơn này.
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

            return RedirectToAction(nameof(Classify), new { id = order.OrderCode });
        }

        // ==========================================
        // ACTION CŨ - KHÔNG CHO CẬP NHẬT TRẠNG THÁI THỦ CÔNG
        // Trạng thái được đổi khi phân công hoặc xử lý đơn thất bại.
        // Giữ action để form cũ không thể thay đổi trạng thái trái quy tắc.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] =
                "Không cập nhật trạng thái thủ công tại màn hình phân loại. " +
                "ReceptionStaff tiếp nhận đơn; khi kho phân công shipper, " +
                "hệ thống tự chuyển đơn sang trạng thái đang giao.";

            if (order.ReceivedAt.HasValue && order.CurrentStatus == ReadyStatus)
            {
                return RedirectToAction(nameof(Classify), new { id = order.OrderCode });
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // TẢI DANH SÁCH LOẠI HÀNG HÓA
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
