using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "ReceptionStaff")]
    public class ReceptionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReceptionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // DANH SÁCH ĐƠN CẦN TIẾP NHẬN
        // =========================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .Where(o => o.CurrentStatus != "Đã hủy")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // =========================
        // MỞ TRANG GHI NHẬN
        // =========================


        [HttpGet]
        public async Task<IActionResult> Record(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
                return NotFound();

            if (order.CurrentStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể ghi nhận đơn hàng đã hủy.";
                return RedirectToAction(nameof(Index));
            }

            var model = new ReceptionViewModel
            {
                OrderCode = order.OrderCode,
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                ReceiverAddress = order.ReceiverAddress,
                ReceptionNote = order.ReceptionNote,

                Items = order.OrderItems
                    .OrderBy(i => i.OrderItemId)
                    .Select(i => new ReceptionItemViewModel
                    {
                        OrderItemId = i.OrderItemId,
                        PackageTypeId = i.PackageTypeId,
                        PackageTypeName = i.PackageType?.TypeName ?? "Chưa phân loại",
                        Description = i.Description ?? string.Empty,
                        Quantity = i.Quantity,
                        Weight = i.Weight,
                        IsFragile = i.IsFragile,
                        IsValuable = i.IsValuable
                    })
                    .ToList()
            };

            // Truyền trạng thái để View biết chỉ xem hay được ghi nhận.
            ViewBag.IsRecorded = order.ReceivedAt.HasValue;
            ViewBag.ReceivedAt = order.ReceivedAt;
            ViewBag.ReceivedBy = order.ReceivedBy;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Record(ReceptionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderCode == model.OrderCode);

            if (order == null)
                return NotFound();

            if (order.CurrentStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể ghi nhận đơn hàng đã hủy.";
                return RedirectToAction(nameof(Index));
            }

            // Đã ghi nhận rồi thì không cho sửa bằng cách gửi POST trực tiếp.
            if (order.ReceivedAt.HasValue)
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng đã được ghi nhận. Chỉ có thể xem hoặc in đơn hàng.";

                return RedirectToAction(
                    nameof(Record),
                    new { id = order.OrderCode });
            }

            if (!ModelState.IsValid)
            {
                // Nạp lại thông tin hiển thị nếu validation thất bại.
                var invalidModel = model;
                invalidModel.Items ??= new List<ReceptionItemViewModel>();

                var savedOrder = await _context.DeliveryOrders
                    .Include(o => o.OrderItems)
                        .ThenInclude(i => i.PackageType)
                    .FirstOrDefaultAsync(o => o.OrderCode == model.OrderCode);

                if (savedOrder != null)
                {
                    foreach (var item in invalidModel.Items)
                    {
                        var savedItem = savedOrder.OrderItems
                            .FirstOrDefault(i => i.OrderItemId == item.OrderItemId);

                        if (savedItem != null)
                            item.PackageTypeName =
                                savedItem.PackageType?.TypeName ?? "Chưa phân loại";
                    }
                }

                ViewBag.IsRecorded = false;
                return View(invalidModel);
            }

            if (model.Items == null || model.Items.Count == 0)
            {
                ModelState.AddModelError("", "Đơn hàng chưa có thông tin hàng hóa.");
                ViewBag.IsRecorded = false;
                return View(model);
            }

            // Cập nhật thông tin người nhận.
            order.ReceiverName = model.ReceiverName;
            order.ReceiverPhone = model.ReceiverPhone;
            order.ReceiverAddress = model.ReceiverAddress;

            // Chỉ cập nhật những mặt hàng thực sự thuộc đơn này.
            foreach (var itemModel in model.Items)
            {
                var item = order.OrderItems.FirstOrDefault(
                    i => i.OrderItemId == itemModel.OrderItemId);

                if (item == null)
                    continue;

                // Description trong database không cho phép NULL.
                item.Description = itemModel.Description ?? string.Empty;
                item.Quantity = itemModel.Quantity;
                item.Weight = itemModel.Weight;
                item.IsFragile = itemModel.IsFragile;
                item.IsValuable = itemModel.IsValuable;
            }

            order.ReceivedBy = user.Id;
            order.ReceivedAt = DateTime.Now;
            order.ReceptionNote = model.ReceptionNote;
            // Sau khi tiếp nhận ghi nhận xong, đơn sẵn sàng để kho điều phối.
            order.CurrentStatus = "Đã nhận - Chưa giao";

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine("[RECEPTION] Lỗi khi lưu thông tin tiếp nhận:");
                Console.WriteLine(ex.ToString());
                throw;
            }

            TempData["SuccessMessage"] =
                $"Đã ghi nhận thông tin đơn {order.OrderCode}.";

            return RedirectToAction(nameof(Index));
        }

    }
}
