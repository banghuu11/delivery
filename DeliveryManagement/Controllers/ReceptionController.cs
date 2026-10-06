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
            {
                return NotFound();
            }

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.CurrentStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] =
                    "Không thể ghi nhận đơn hàng đã hủy.";

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

                        Description = i.Description,
                        Size = i.Size,
                        Quantity = i.Quantity,
                        Weight = i.Weight,

                        IsFragile = i.IsFragile,
                        IsValuable = i.IsValuable
                    })
                    .ToList()
            };

            return View(model);
        }

        // =========================
        // LƯU GHI NHẬN
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Record(ReceptionViewModel model)
        {
            if (!ModelState.IsValid)
            {
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
                    "Không thể ghi nhận đơn hàng đã hủy.";

                return RedirectToAction(nameof(Index));
            }

            // =========================
            // CẬP NHẬT THÔNG TIN NGƯỜI NHẬN
            // =========================

            order.ReceiverName = model.ReceiverName;
            order.ReceiverPhone = model.ReceiverPhone;
            order.ReceiverAddress = model.ReceiverAddress;

            // =========================
            // CẬP NHẬT THÔNG TIN HÀNG HÓA
            // =========================

            foreach (var itemModel in model.Items)
            {
                var item = order.OrderItems
                    .FirstOrDefault(i =>
                        i.OrderItemId == itemModel.OrderItemId);

                if (item == null)
                {
                    continue;
                }

                // ReceptionStaff được chỉnh thông tin thực tế
                // nhưng KHÔNG thay đổi PackageTypeId.
                // PackageTypeId để WarehouseStaff xử lý ở #14.

                item.Description = itemModel.Description;
                item.Size = itemModel.Size;
                item.Quantity = itemModel.Quantity;
                item.Weight = itemModel.Weight;
                item.IsFragile = itemModel.IsFragile;
                item.IsValuable = itemModel.IsValuable;
            }

            // =========================
            // THÔNG TIN TIẾP NHẬN
            // =========================

            order.ReceivedBy = user.Id;
            order.ReceivedAt = DateTime.Now;
            order.ReceptionNote = model.ReceptionNote;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã ghi nhận thông tin đơn {order.OrderCode}.";

            return RedirectToAction(nameof(Index));
        }
    }
}