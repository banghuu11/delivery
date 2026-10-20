using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize]
    public class DeliveryOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPricingService _pricingService;
        private readonly IRouteDistanceService _routeDistanceService;

        public DeliveryOrderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPricingService pricingService,
            IRouteDistanceService routeDistanceService)
        {
            _context = context;
            _userManager = userManager;
            _pricingService = pricingService;
            _routeDistanceService = routeDistanceService;
        }

        // ==========================================
        // LOAD LOẠI HÀNG HÓA
        // ==========================================

        private async Task LoadPackageTypesAsync()
        {
            ViewBag.PackageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();
        }

        private async Task LoadSenderAddressesAsync(string userId)
        {
            ViewBag.SenderAddresses = await _context.SenderAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        // ==========================================
        // DANH SÁCH ĐƠN HÀNG CỦA KHÁCH HÀNG
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

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
                return Challenge();

            var model = new CreateOrderViewModel
            {
                SenderName = user.FullName,
                SenderPhone = user.PhoneNumber ?? string.Empty,
                SenderAddress = user.DefaultSenderAddress ?? string.Empty,
                DistanceKm = 0,

                Items = new List<OrderItemInputModel>
                {
                    new OrderItemInputModel()
                }
            };

            await LoadPackageTypesAsync();
            await LoadSenderAddressesAsync(user.Id);

            return View(model);
        }

        // ==========================================
        // TẠO ĐƠN - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderViewModel model)
        {
            Console.WriteLine();
            Console.WriteLine("========== [CREATE ORDER] BẮT ĐẦU ==========");

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                Console.WriteLine(
                    "[CREATE ORDER] LỖI: Không lấy được người dùng đăng nhập.");

                return Challenge();
            }

            Console.WriteLine(
                $"[CREATE ORDER] UserId = {user.Id}");

            // DistanceKm do server tính, không tin giá trị hidden từ trình duyệt.
            ModelState.Remove(nameof(model.DistanceKm));

            // DEBUG 1: Kiểm tra dữ liệu gửi lên.
            Console.WriteLine(
                $"[CREATE ORDER] ModelState.IsValid = {ModelState.IsValid}");

            Console.WriteLine(
                $"[CREATE ORDER] SenderName có dữ liệu = {!string.IsNullOrWhiteSpace(model.SenderName)}");

            Console.WriteLine(
                $"[CREATE ORDER] SenderPhone có dữ liệu = {!string.IsNullOrWhiteSpace(model.SenderPhone)}");

            Console.WriteLine(
                $"[CREATE ORDER] SenderAddress có dữ liệu = {!string.IsNullOrWhiteSpace(model.SenderAddress)}");

            Console.WriteLine(
                $"[CREATE ORDER] ReceiverName có dữ liệu = {!string.IsNullOrWhiteSpace(model.ReceiverName)}");

            Console.WriteLine(
                $"[CREATE ORDER] ReceiverPhone có dữ liệu = {!string.IsNullOrWhiteSpace(model.ReceiverPhone)}");

            Console.WriteLine(
                $"[CREATE ORDER] ReceiverAddress có dữ liệu = {!string.IsNullOrWhiteSpace(model.ReceiverAddress)}");

            Console.WriteLine(
                $"[CREATE ORDER] DeliveryMethod = {model.DeliveryMethod}");

            Console.WriteLine(
                $"[CREATE ORDER] PaymentMethod = {model.PaymentMethod}");

            Console.WriteLine(
                $"[CREATE ORDER] Items count = {model.Items?.Count ?? 0}");

            // In tất cả lỗi ModelState để xác định trường nào không hợp lệ.
            foreach (var entry in ModelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    Console.WriteLine(
                        $"[CREATE ORDER] VALIDATION ERROR [{entry.Key}]: " +
                        $"{error.ErrorMessage} " +
                        $"{error.Exception?.Message}");
                }
            }

            if (!ModelState.IsValid)
            {
                Console.WriteLine(
                    "[CREATE ORDER] DỪNG TẠI BƯỚC 1: ModelState không hợp lệ.");

                await LoadPackageTypesAsync();
                await LoadSenderAddressesAsync(user.Id);

                return View(model);
            }

            Console.WriteLine(
                "[CREATE ORDER] BƯỚC 1 THÀNH CÔNG: ModelState hợp lệ.");

            // ==========================================
            // KIỂM TRA HÀNG HÓA
            // ==========================================

            if (model.Items == null || model.Items.Count == 0)
            {
                Console.WriteLine(
                    "[CREATE ORDER] DỪNG: Đơn hàng không có hàng hóa.");

                ModelState.AddModelError(
                    nameof(model.Items),
                    "Đơn hàng phải có ít nhất một hàng hóa.");

                await LoadPackageTypesAsync();
                await LoadSenderAddressesAsync(user.Id);

                return View(model);
            }

            if (model.Items.Any(i => i.Quantity <= 0 || i.Weight <= 0))
            {
                Console.WriteLine(
                    "[CREATE ORDER] DỪNG: Số lượng hoặc khối lượng không hợp lệ.");

                foreach (var item in model.Items)
                {
                    Console.WriteLine(
                        $"[CREATE ORDER] Item: PackageTypeId={item.PackageTypeId}, " +
                        $"Quantity={item.Quantity}, Weight={item.Weight}");
                }

                ModelState.AddModelError(
                    nameof(model.Items),
                    "Số lượng và khối lượng mỗi món phải lớn hơn 0.");

                await LoadPackageTypesAsync();
                await LoadSenderAddressesAsync(user.Id);

                return View(model);
            }

            Console.WriteLine(
                "[CREATE ORDER] BƯỚC 2 THÀNH CÔNG: Hàng hóa hợp lệ.");

            // ==========================================
            // KIỂM TRA LOẠI HÀNG HÓA
            // ==========================================

            var typeIds = model.Items
                .Select(i => i.PackageTypeId)
                .Distinct()
                .ToList();

            Console.WriteLine(
                $"[CREATE ORDER] PackageTypeIds = {string.Join(", ", typeIds)}");

            var validCount = await _context.PackageTypes
                .CountAsync(p =>
                    typeIds.Contains(p.PackageTypeId) &&
                    p.IsActive);

            if (validCount != typeIds.Count)
            {
                Console.WriteLine(
                    $"[CREATE ORDER] DỪNG: Số loại hàng hợp lệ = {validCount}, " +
                    $"số loại hàng yêu cầu = {typeIds.Count}.");

                ModelState.AddModelError(
                    nameof(model.Items),
                    "Có loại hàng hóa không hợp lệ.");

                await LoadPackageTypesAsync();
                await LoadSenderAddressesAsync(user.Id);

                return View(model);
            }

            Console.WriteLine(
                "[CREATE ORDER] BƯỚC 3 THÀNH CÔNG: Loại hàng hóa hợp lệ.");

            // ==========================================
            // TỔNG KHỐI LƯỢNG
            // ==========================================

            var totalWeight = model.Items.Sum(
                i => i.Weight * i.Quantity);

            Console.WriteLine(
                $"[CREATE ORDER] TotalWeight = {totalWeight}");

            if (totalWeight <= 0)
            {
                Console.WriteLine(
                    "[CREATE ORDER] DỪNG: Tổng khối lượng không hợp lệ.");

                ModelState.AddModelError(
                    nameof(model.Items),
                    "Tổng khối lượng phải lớn hơn 0.");

                await LoadPackageTypesAsync();
                await LoadSenderAddressesAsync(user.Id);

                return View(model);
            }

            // ==========================================
            // TÍNH KHOẢNG CÁCH
            // ==========================================

            Console.WriteLine(
                "[CREATE ORDER] BẮT ĐẦU tính khoảng cách bằng service.");

            var routeResult =
                await _routeDistanceService.CalculateDistanceKmAsync(
                    model.SenderAddress,
                    model.ReceiverAddress,
                    null,
                    null);

            Console.WriteLine(
                $"[CREATE ORDER] Route Success = {routeResult.Success}");

            Console.WriteLine(
                $"[CREATE ORDER] Route DistanceKm = {routeResult.DistanceKm}");

            Console.WriteLine(
                $"[CREATE ORDER] Route Message = {routeResult.Message}");

            if (!routeResult.Success || routeResult.DistanceKm <= 0)
            {
                Console.WriteLine(
                    "[CREATE ORDER] DỪNG: Không tính được khoảng cách hợp lệ.");

                ModelState.AddModelError(
                    nameof(model.ReceiverAddress),
                    string.IsNullOrWhiteSpace(routeResult.Message)
                        ? "Không thể tính khoảng cách giữa hai địa chỉ."
                        : routeResult.Message);

                await LoadPackageTypesAsync();
                await LoadSenderAddressesAsync(user.Id);

                return View(model);
            }

            var distanceKm = routeResult.DistanceKm;

            model.DistanceKm = distanceKm;

            var senderLatitude = routeResult.SenderLatitude;
            var senderLongitude = routeResult.SenderLongitude;

            Console.WriteLine(
                $"[CREATE ORDER] Khoảng cách hợp lệ: {distanceKm} km.");

            // ==========================================
            // TẠO MÃ ĐƠN HÀNG
            // ==========================================

            string orderCode;

            do
            {
                orderCode = $"DH{DateTime.Now:yyyyMMddHHmmssfff}";
            }
            while (await _context.DeliveryOrders
                .AnyAsync(o => o.OrderCode == orderCode));

            Console.WriteLine(
                $"[CREATE ORDER] OrderCode = {orderCode}");

            // ==========================================
            // TÍNH GIÁ
            // ==========================================

            Console.WriteLine(
                "[CREATE ORDER] BẮT ĐẦU tính giá.");

            var priceCalc = await _pricingService.CalculatePriceAsync(
                model.DeliveryMethod,
                totalWeight,
                distanceKm,
                senderLatitude,
                senderLongitude);

            Console.WriteLine(
                $"[CREATE ORDER] PriceCalc.Success = {priceCalc.Success}");

            Console.WriteLine(
                $"[CREATE ORDER] PriceCalc.TotalPrice = {priceCalc.TotalPrice}");

            // ==========================================
            // TẠO DELIVERY ORDER
            // ==========================================

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
                DistanceKm = distanceKm,

                PaymentMethod = model.PaymentMethod,
                PaymentStatus = "Chưa thanh toán",
                PaymentAmount = null,
                PaymentTime = null,

                TotalAmount = priceCalc.TotalPrice,
                BaseShippingFee = priceCalc.BaseShippingFee,
                WeightFee = priceCalc.WeightFee,
                WeatherFee = priceCalc.WeatherFee,
                TrafficFee = priceCalc.TrafficFee,
                WeatherDescription = priceCalc.WeatherDescription,
                TrafficDescription = priceCalc.TrafficDescription,

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

            // ==========================================
            // TẠO ORDER ITEMS
            // ==========================================

            var orderItems = model.Items
                .Select(i => new OrderItem
                {
                    OrderCode = orderCode,

                    OriginalPackageTypeId = i.PackageTypeId,
                    PackageTypeId = i.PackageTypeId,

                    Description = i.Description ?? string.Empty,
                    Quantity = i.Quantity,
                    Weight = i.Weight,
                    IsFragile = i.IsFragile,
                    IsValuable = i.IsValuable
                })
                .ToList();

            // ==========================================
            // LỊCH SỬ TRẠNG THÁI BAN ĐẦU
            // ==========================================

            var statusHistory = new OrderStatusHistory
            {
                OrderCode = orderCode,
                Status = "Chưa nhận",
                ChangedBy = user.Id,
                ChangedAt = DateTime.Now
            };

            _context.DeliveryOrders.Add(order);
            _context.OrderItems.AddRange(orderItems);
            _context.OrderStatusHistories.Add(statusHistory);

            Console.WriteLine(
                "[CREATE ORDER] ĐANG LƯU DeliveryOrder, OrderItems và StatusHistory...");

            try
            {
                await _context.SaveChangesAsync();

                Console.WriteLine(
                    $"[CREATE ORDER] LƯU THÀNH CÔNG. OrderCode = {order.OrderCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[CREATE ORDER] LỖI KHI LƯU DATABASE:");

                Console.WriteLine(ex.ToString());

                throw;
            }

            // ==========================================
            // CẬP NHẬT THÔNG TIN NGƯỜI GỬI MẶC ĐỊNH
            // ==========================================

            Console.WriteLine(
                "[CREATE ORDER] BẮT ĐẦU cập nhật thông tin người gửi.");

            user.DefaultSenderAddress = model.SenderAddress;

            if (string.IsNullOrWhiteSpace(user.FullName))
                user.FullName = model.SenderName;

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                user.PhoneNumber = model.SenderPhone;

            var updateUserResult = await _userManager.UpdateAsync(user);

            if (!updateUserResult.Succeeded)
            {
                Console.WriteLine(
                    "[CREATE ORDER] CẢNH BÁO: Cập nhật người dùng không thành công.");

                foreach (var error in updateUserResult.Errors)
                {
                    Console.WriteLine(
                        $"[CREATE ORDER] USER UPDATE ERROR: {error.Code} - {error.Description}");
                }
            }

            // ==========================================
            // LƯU SỔ ĐỊA CHỈ NẾU NGƯỜI DÙNG CHỌN
            // ==========================================

            if (model.SaveToAddressBook)
            {
                Console.WriteLine(
                    "[CREATE ORDER] Người dùng chọn lưu sổ địa chỉ.");

                var exists = await _context.SenderAddresses
                    .AnyAsync(a =>
                        a.UserId == user.Id &&
                        a.SenderAddressText == model.SenderAddress);

                if (!exists)
                {
                    var newAddress = new SenderAddress
                    {
                        UserId = user.Id,
                        SenderName = model.SenderName,
                        SenderPhone = model.SenderPhone,
                        SenderAddressText = model.SenderAddress,

                        AddressTag = string.IsNullOrWhiteSpace(model.AddressTag)
                            ? "Điểm gửi hàng"
                            : model.AddressTag,

                        IsDefault = false,
                        CreatedAt = DateTime.Now
                    };

                    _context.SenderAddresses.Add(newAddress);

                    await _context.SaveChangesAsync();

                    Console.WriteLine(
                        "[CREATE ORDER] Lưu địa chỉ vào sổ địa chỉ thành công.");
                }
                else
                {
                    Console.WriteLine(
                        "[CREATE ORDER] Địa chỉ đã có trong sổ địa chỉ.");
                }
            }

            // ==========================================
            // THÔNG BÁO VÀ CHUYỂN TRANG
            // ==========================================

            TempData[priceCalc.Success
                ? "SuccessMessage"
                : "ErrorMessage"] = priceCalc.Success
                ? $"Tạo yêu cầu giao hàng thành công. Mã đơn: {orderCode}"
                : $"Tạo yêu cầu giao hàng thành công. Mã đơn: {orderCode}. Chưa có mức giá phù hợp trong bảng giá.";

            Console.WriteLine(
                $"[CREATE ORDER] HOÀN TẤT. Chuyển đến Details với mã đơn {orderCode}.");

            Console.WriteLine(
                "========== [CREATE ORDER] KẾT THÚC ==========");

            return RedirectToAction(
                nameof(Details),
                new { id = orderCode });
        }

        // ==========================================
        // API TÍNH GIÁ DỰ KIẾN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> CalculatePrice(
            string deliveryMethod,
            decimal totalWeight,
            decimal distanceKm,
            double? lat = null,
            double? lon = null)
        {
            if (string.IsNullOrWhiteSpace(deliveryMethod) ||
                totalWeight <= 0 ||
                distanceKm <= 0)
            {
                return Json(new
                {
                    success = false,
                    price = 0,
                    message =
                        "Vui lòng chọn hình thức giao hàng, xác định khoảng cách và nhập khối lượng hợp lệ."
                });
            }

            var result = await _pricingService.CalculatePriceAsync(
                deliveryMethod,
                totalWeight,
                distanceKm,
                lat,
                lon);

            return Json(new
            {
                success = result.Success,
                price = result.TotalPrice,
                distanceKm,

                baseShippingFee = result.BaseShippingFee,
                weightFee = result.WeightFee,
                weatherFee = result.WeatherFee,
                trafficFee = result.TrafficFee,

                weatherDescription = result.WeatherDescription,
                trafficDescription = result.TrafficDescription,

                weatherIcon = result.WeatherIcon,
                trafficIcon = result.TrafficIcon,

                currentTemperature = result.CurrentTemperature,
                currentRainMm = result.CurrentRainMm,

                isWeatherSurchargeApplied =
                    result.IsWeatherSurchargeApplied,

                isTrafficSurchargeApplied =
                    result.IsTrafficSurchargeApplied
            });
        }

        // ==========================================
        // API TÍNH KHOẢNG CÁCH CHO GIAO DIỆN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> CalculateRouteDistance(
            [FromQuery] string senderAddress,
            [FromQuery] string receiverAddress)
        {
            if (string.IsNullOrWhiteSpace(senderAddress) ||
                string.IsNullOrWhiteSpace(receiverAddress))
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Vui lòng nhập đầy đủ địa chỉ gửi và địa chỉ nhận."
                });
            }

            try
            {
                var routeResult =
                    await _routeDistanceService.CalculateDistanceKmAsync(
                        senderAddress,
                        receiverAddress,
                        null,
                        null);

                if (!routeResult.Success || routeResult.DistanceKm <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = string.IsNullOrWhiteSpace(routeResult.Message)
                            ? "Không thể tính khoảng cách giữa hai địa chỉ."
                            : routeResult.Message
                    });
                }

                return Json(new
                {
                    success = true,
                    distanceKm = routeResult.DistanceKm,
                    senderLat = routeResult.SenderLatitude,
                    senderLon = routeResult.SenderLongitude,
                    message = routeResult.Message
                });
            }
            catch (HttpRequestException)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Không kết nối được dịch vụ định tuyến. Vui lòng thử lại."
                });
            }
            catch (TaskCanceledException)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Dịch vụ định tuyến hết thời gian chờ. Vui lòng thử lại."
                });
            }
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
                    p.MinWeight < totalWeight &&
                    totalWeight <= p.MaxWeight &&
                    p.MinDistance < distanceKm &&
                    distanceKm <= p.MaxDistance)
                .OrderBy(p => p.MinWeight)
                .ThenBy(p => p.MinDistance)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync();
        }

        // ==========================================
        // CHI TIẾT ĐƠN HÀNG
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var isAdmin = User.IsInRole("Admin");
            var isReceptionStaff = User.IsInRole("ReceptionStaff");
            var isWarehouseStaff = User.IsInRole("WarehouseStaff");
            var isDeliveryStaff = User.IsInRole("DeliveryStaff");
            var isCustomer = User.IsInRole("Customer");

            if (!isAdmin &&
                !isReceptionStaff &&
                !isWarehouseStaff &&
                !isDeliveryStaff &&
                !isCustomer)
            {
                return Forbid();
            }

            var query = _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .AsQueryable();

            if (!isAdmin && !isReceptionStaff && !isWarehouseStaff)
            {
                if (isDeliveryStaff)
                {
                    query = query.Where(o => o.DeliveryStaffId == user.Id);
                }
                else if (isCustomer)
                {
                    query = query.Where(o => o.CustomerId == user.Id);
                }
            }

            var order = await query
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn này.";

                return RedirectToAction("Index", "Home");
            }

            return View(order);
        }

        // ==========================================
        // XEM VÀ IN PHIẾU GỬI HÀNG / VẬN ĐƠN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> PrintWaybill(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var isAdmin = User.IsInRole("Admin");
            var isReceptionStaff = User.IsInRole("ReceptionStaff");
            var isWarehouseStaff = User.IsInRole("WarehouseStaff");
            var isDeliveryStaff = User.IsInRole("DeliveryStaff");
            var isCustomer = User.IsInRole("Customer");

            if (!isAdmin &&
                !isReceptionStaff &&
                !isWarehouseStaff &&
                !isDeliveryStaff &&
                !isCustomer)
            {
                return Forbid();
            }

            var query = _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .AsQueryable();

            if (!isAdmin && !isReceptionStaff && !isWarehouseStaff)
            {
                if (isDeliveryStaff)
                {
                    query = query.Where(o => o.DeliveryStaffId == user.Id);
                }
                else if (isCustomer)
                {
                    query = query.Where(o => o.CustomerId == user.Id);
                }
            }

            var order = await query
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng hoặc bạn không có quyền in phiếu gửi hàng.";

                return RedirectToAction("Index", "Home");
            }

            return View("PrintWaybill", order);
        }

        // ==========================================
        // ĐƯỜNG DẪN INVOICE - DÙNG CHUNG PHÂN QUYỀN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Invoice(string id)
        {
            return await PrintWaybill(id);
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
                return Challenge();

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";

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

            if (order.CurrentStatus != "Chưa nhận")
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng không còn ở trạng thái có thể hủy.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = order.OrderCode });
            }

            order.CurrentStatus = "Đã hủy";

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

            return RedirectToAction(
                nameof(Details),
                new { id = order.OrderCode });
        }
    }
}