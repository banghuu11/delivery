using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "Customer")]
    public class DeliveryOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DeliveryOrderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // HIỂN THỊ FORM TẠO ĐƠN
        // =========================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var model = new CreateOrderViewModel
            {
                SenderName = user.FullName,
                SenderPhone = user.PhoneNumber ?? string.Empty,
                SenderAddress = user.DefaultSenderAddress ?? string.Empty
            };

            ViewBag.PackageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();

            return View(model);
        }


        // =========================
        // XỬ LÝ TẠO ĐƠN
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateOrderViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.PackageTypes = await _context.PackageTypes
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.TypeName)
                    .ToListAsync();

                return View(model);
            }

            var packageTypeExists = await _context.PackageTypes
                .AnyAsync(p =>
                    p.PackageTypeId == model.PackageTypeId &&
                    p.IsActive);

            if (!packageTypeExists)
            {
                ModelState.AddModelError(
                    nameof(model.PackageTypeId),
                    "Loại đóng gói không hợp lệ.");

                ViewBag.PackageTypes = await _context.PackageTypes
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.TypeName)
                    .ToListAsync();

                return View(model);
            }

            // =========================
            // TẠO MÃ ĐƠN
            // =========================

            string orderCode;

            do
            {
                orderCode =
                    $"DH{DateTime.Now:yyyyMMddHHmmssfff}";
            }
            while (await _context.DeliveryOrders
                .AnyAsync(o => o.OrderCode == orderCode));


            // =========================
            // TẠO DELIVERY ORDER
            // =========================

            var order = new DeliveryOrder
            {
                OrderCode = orderCode,

                CustomerId = user.Id,

                SenderName = model.SenderName,
                SenderPhone = model.SenderPhone,
                SenderAddress = model.SenderAddress,

                ReceiverName = model.ReceiverName,
                ReceiverPhone = model.ReceiverPhone,
                ReceiverAddress = model.ReceiverAddress,

                DeliveryMethod = model.DeliveryMethod,

                PaymentMethod = model.PaymentMethod,
                PaymentStatus = "Chưa thanh toán",

                TotalAmount = 0,

                CurrentStatus = "Chưa nhận",

                CreatedAt = DateTime.Now
            };


            // =========================
            // TẠO ORDER ITEM
            // =========================

            var orderItem = new OrderItem
            {
                OrderCode = orderCode,

                PackageTypeId = model.PackageTypeId,

                Description = model.Description,
                Size = model.Size,

                Quantity = model.Quantity,

                Weight = model.Weight,

                IsFragile = model.IsFragile,
                IsValuable = model.IsValuable
            };


            // =========================
            // LƯU LỊCH SỬ TRẠNG THÁI
            // =========================

            var statusHistory = new OrderStatusHistory
            {
                OrderCode = orderCode,

                Status = "Chưa nhận",

                ChangedBy = user.Id,

                ChangedAt = DateTime.Now
            };


            // =========================
            // LƯU DATABASE
            // =========================

            _context.DeliveryOrders.Add(order);
            _context.OrderItems.Add(orderItem);
            _context.OrderStatusHistories.Add(statusHistory);

            await _context.SaveChangesAsync();


            // =========================
            // CẬP NHẬT THÔNG TIN NGƯỜI GỬI
            // =========================

            user.DefaultSenderAddress = model.SenderAddress;

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = model.SenderName;
            }

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                user.PhoneNumber = model.SenderPhone;
            }

            await _userManager.UpdateAsync(user);


            TempData["SuccessMessage"] =
                $"Tạo yêu cầu giao hàng thành công. Mã đơn: {orderCode}";

            return RedirectToAction(  "Details",  new { id = orderCode });
        }
        // =========================
        // XEM CHI TIẾT ĐƠN HÀNG
        // =========================

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1. Lấy thông tin đơn hàng gốc
            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null) return NotFound();

            // 2. Lấy chi tiết Hàng hóa & Phân loại hàng hóa (Phần của bạn)
            var orderItem = await _context.OrderItems
                .Include(oi => oi.PackageType) // Gọi bảng PackageType để lấy tên
                .FirstOrDefaultAsync(oi => oi.OrderCode == id);

            // Truyền qua ViewBag
            ViewBag.OrderItem = orderItem;

            return View(order);
        }

        // =========================
        // HỦY YÊU CẦU GIAO HÀNG
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string id)
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

                return RedirectToAction("Index", "Home");
            }

            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.CustomerId == user.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng hoặc bạn không có quyền hủy đơn này.";

                return RedirectToAction("Index", "Home");
            }

            // Chỉ cho phép hủy khi đơn chưa được tiếp nhận
            if (order.CurrentStatus != "Chưa nhận")
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng không còn ở trạng thái có thể hủy.";

                return RedirectToAction("Index", "Home");
            }

            // Cập nhật trạng thái đơn
            order.CurrentStatus = "Đã hủy";

            // Ghi lịch sử trạng thái
            var statusHistory = new OrderStatusHistory
            {
                OrderCode = order.OrderCode,
                Status = "Đã hủy",
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            };

            _context.OrderStatusHistories.Add(statusHistory);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã hủy yêu cầu giao hàng {order.OrderCode}.";

            return RedirectToAction("Details",new { id = order.OrderCode });
        }

        // =========================
        // CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string newStatus)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Tìm đơn hàng theo mã (OrderCode)
            var order = await _context.DeliveryOrders.FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "Home");
            }
            string[] validStatuses = { "Chưa nhận", "Đã nhận - Chưa giao", "Đã nhận – Đang giao", "Đã Giao" };

            if (validStatuses.Contains(newStatus))
            {
                order.CurrentStatus = newStatus;
                var statusHistory = new OrderStatusHistory
                {
                    OrderCode = order.OrderCode,
                    Status = newStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                };

                _context.DeliveryOrders.Update(order);
                _context.OrderStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn {order.OrderCode} thành: {newStatus}";
            }
            return RedirectToAction("Details", new { id = order.OrderCode });
        }
    }
}