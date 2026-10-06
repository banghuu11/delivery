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

        // ==========================================
        // LOAD DANH SÁCH LOẠI HÀNG HÓA
        // ==========================================

        private async Task LoadPackageTypesAsync()
        {
            ViewBag.PackageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();
        }

        // ==========================================
        // DANH SÁCH ĐƠN HÀNG CỦA TÔI
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var query = _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == user.Id);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.CurrentStatus == status);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            return View(orders);
        }

        // ==========================================
        // TẠO ĐƠN - GET
        // ==========================================

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
                SenderAddress = user.DefaultSenderAddress ?? string.Empty,

                Items = new List<OrderItemInputModel>
                {
                    new OrderItemInputModel()
                }
            };

            await LoadPackageTypesAsync();

            return View(model);
        }

        // ==========================================
        // TẠO ĐƠN - POST
        // ==========================================

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

            // ------------------------------------------
            // KIỂM TRA MODEL
            // ------------------------------------------

            if (!ModelState.IsValid)
            {
                await LoadPackageTypesAsync();
                return View(model);
            }

            // ------------------------------------------
            // KIỂM TRA DANH SÁCH HÀNG HÓA
            // ------------------------------------------

            if (model.Items == null || model.Items.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(model.Items),
                    "Đơn hàng phải có ít nhất một hàng hóa.");

                await LoadPackageTypesAsync();
                return View(model);
            }

            // ------------------------------------------
            // KIỂM TRA PACKAGE TYPE
            // ------------------------------------------

            var typeIds = model.Items
                .Select(i => i.PackageTypeId)
                .Distinct()
                .ToList();

            var validCount = await _context.PackageTypes
                .CountAsync(p =>
                    typeIds.Contains(p.PackageTypeId) &&
                    p.IsActive);

            if (validCount != typeIds.Count)
            {
                ModelState.AddModelError(
                    nameof(model.Items),
                    "Có loại hàng hóa không hợp lệ.");

                await LoadPackageTypesAsync();
                return View(model);
            }

            // ------------------------------------------
            // TỔNG KHỐI LƯỢNG
            // ------------------------------------------

            var totalWeight = model.Items.Sum(
                i => i.Weight * i.Quantity);

            if (totalWeight <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.Items),
                    "Tổng khối lượng phải lớn hơn 0.");

                await LoadPackageTypesAsync();
                return View(model);
            }

            // ------------------------------------------
            // KIỂM TRA KHOẢNG CÁCH
            // ------------------------------------------

            if (model.DistanceKm <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.DistanceKm),
                    "Khoảng cách phải lớn hơn 0 km.");

                await LoadPackageTypesAsync();
                return View(model);
            }

            // ------------------------------------------
            // TẠO MÃ ĐƠN
            // ------------------------------------------

            string orderCode;

            do
            {
                orderCode =
                    $"DH{DateTime.Now:yyyyMMddHHmmssfff}";
            }
            while (await _context.DeliveryOrders
                .AnyAsync(o => o.OrderCode == orderCode));

            // ------------------------------------------
            // TÌM GIÁ THEO:
            // Hình thức + Khối lượng + Khoảng cách
            // ------------------------------------------

            var price = await FindPriceAsync(
                model.DeliveryMethod,
                totalWeight,
                model.DistanceKm);

            // ------------------------------------------
            // TẠO DELIVERY ORDER
            // ------------------------------------------

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
                DistanceKm = model.DistanceKm,

                PaymentMethod = model.PaymentMethod,
                PaymentStatus = "Chưa thanh toán",

                PaymentAmount = null,
                PaymentTime = null,

                TotalAmount = price ?? 0,

                CurrentStatus = "Chưa nhận",

                DeliveryStaffId = null,
                DeliveryResult = null,
                ProofImage = null,
                DeliveryNote = null,

                ReceivedBy = null,
                ReceivedAt = null,
                ReceptionNote = null,

                CreatedAt = DateTime.Now
            };

            // ------------------------------------------
            // TẠO ORDER ITEMS
            // ------------------------------------------

            var orderItems = model.Items
                .Select(i => new OrderItem
                {
                    OrderCode = orderCode,

                    // Loại khách khai báo ban đầu
                    OriginalPackageTypeId = i.PackageTypeId,

                    // Ban đầu loại thực tế = loại khách khai báo
                    // Sau này WarehouseStaff có thể thay đổi
                    PackageTypeId = i.PackageTypeId,

                    Description = i.Description,
                    Size = i.Size,
                    Quantity = i.Quantity,
                    Weight = i.Weight,
                    IsFragile = i.IsFragile,
                    IsValuable = i.IsValuable
                })
                .ToList();

            // ------------------------------------------
            // LƯU LỊCH SỬ TRẠNG THÁI BAN ĐẦU
            // ------------------------------------------

            var statusHistory = new OrderStatusHistory
            {
                OrderCode = orderCode,
                Status = "Chưa nhận",
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            };

            // ------------------------------------------
            // ADD DB
            // ------------------------------------------

            _context.DeliveryOrders.Add(order);
            _context.OrderItems.AddRange(orderItems);
            _context.OrderStatusHistories.Add(statusHistory);

            await _context.SaveChangesAsync();

            // ------------------------------------------
            // CẬP NHẬT THÔNG TIN NGƯỜI GỬI MẶC ĐỊNH
            // ------------------------------------------

            user.DefaultSenderAddress =
                model.SenderAddress;

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = model.SenderName;
            }

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                user.PhoneNumber = model.SenderPhone;
            }

            await _userManager.UpdateAsync(user);

            // ------------------------------------------
            // THÔNG BÁO
            // ------------------------------------------

            TempData["SuccessMessage"] = price == null
                ? $"Tạo yêu cầu giao hàng thành công. " +
                  $"Mã đơn: {orderCode}. " +
                  $"Chưa có mức giá phù hợp trong bảng giá."
                : $"Tạo yêu cầu giao hàng thành công. " +
                  $"Mã đơn: {orderCode}";

            // ------------------------------------------
            // CHUYỂN SANG DETAILS
            // ------------------------------------------

            return RedirectToAction(
                nameof(Details),
                new { id = orderCode });
        }

        // ==========================================
        // API TÍNH GIÁ TRƯỚC KHI TẠO ĐƠN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> CalculatePrice(
            string deliveryMethod,
            decimal totalWeight,
            decimal distanceKm)
        {
            if (string.IsNullOrWhiteSpace(deliveryMethod) ||
                totalWeight <= 0 ||
                distanceKm <= 0)
            {
                return Json(new
                {
                    success = false,
                    price = 0
                });
            }

            var price = await FindPriceAsync(
                deliveryMethod,
                totalWeight,
                distanceKm);

            return Json(new
            {
                success = price.HasValue,
                price = price ?? 0
            });
        }

        // ==========================================
        // TÌM GIÁ TRONG PRICE TABLE
        // ==========================================

        private async Task<decimal?> FindPriceAsync(
            string deliveryMethod,
            decimal totalWeight,
            decimal distanceKm)
        {
            return await _context.PriceTables
                .Where(p =>
                    p.DeliveryMethod == deliveryMethod &&

                    // Khối lượng:
                    // Min < Weight <= Max
                    p.MinWeight < totalWeight &&
                    totalWeight <= p.MaxWeight &&

                    // Khoảng cách:
                    // Min < Distance <= Max
                    p.MinDistance < distanceKm &&
                    distanceKm <= p.MaxDistance)
                .OrderBy(p => p.MinWeight)
                .ThenBy(p => p.MinDistance)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync();
        }

        // ==========================================
        // XEM CHI TIẾT ĐƠN HÀNG
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
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.CustomerId == user.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng " +
                    "hoặc bạn không có quyền xem đơn này.";

                return RedirectToAction(
                    "Index",
                    "Home");
            }

            return View(order);
        }

        // ==========================================
        // HỦY ĐƠN HÀNG
        // ==========================================

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

                return RedirectToAction(
                    "Index",
                    "Home");
            }

            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.CustomerId == user.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng " +
                    "hoặc bạn không có quyền hủy đơn này.";

                return RedirectToAction(
                    "Index",
                    "Home");
            }

            // ------------------------------------------
            // CHỈ ĐƯỢC HỦY KHI CHƯA NHẬN
            // ------------------------------------------

            if (order.CurrentStatus != "Chưa nhận")
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng không còn ở trạng thái " +
                    "có thể hủy.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = order.OrderCode });
            }

            // ------------------------------------------
            // CẬP NHẬT TRẠNG THÁI
            // ------------------------------------------

            order.CurrentStatus = "Đã hủy";

            var statusHistory = new OrderStatusHistory
            {
                OrderCode = order.OrderCode,
                Status = "Đã hủy",
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            };

            _context.OrderStatusHistories.Add(
                statusHistory);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã hủy yêu cầu giao hàng " +
                $"{order.OrderCode}.";

            return RedirectToAction(
                nameof(Details),
                new { id = order.OrderCode });
        }
    }
}