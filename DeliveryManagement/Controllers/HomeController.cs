using System.Diagnostics;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Lấy cài đặt website từ DB
            var setting = await _context.WebsiteSettings.FirstOrDefaultAsync() ?? new WebsiteSetting();

            // 2. Lấy 3 mục Dock tính năng từ DB
            var dockFeatures = await _context.HomeFeatures
                .Where(f => f.IsActive && f.SectionType == "Dock")
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();

            // 3. Lấy danh sách dịch vụ chính từ DB
            var mainServices = await _context.HomeFeatures
                .Where(f => f.IsActive && f.SectionType == "Service")
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();

            // 4. Lấy danh mục loại hàng hóa từ DB
            var packageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();

            // 5. Lấy một số mốc giá nổi bật từ DB
            var priceHighlights = await _context.PriceTables
                .Take(6)
                .ToListAsync();

            // 6. Lấy danh sách bưu cục / trạm trung chuyển từ DB
            var stations = await _context.MapStations
                .Where(s => s.IsActive)
                .Take(5)
                .ToListAsync();

            // 7. Lấy đánh giá khách hàng từ DB
            var reviews = await _context.Reviews
                .Include(r => r.Customer)
                .Take(3)
                .ToListAsync();

            // 8. Thống kê động từ DB
            var totalOrders = await _context.DeliveryOrders.CountAsync();
            var totalDelivered = await _context.DeliveryOrders
                .CountAsync(o => o.CurrentStatus == "Đã giao" || o.CurrentStatus == "Giao thành công");
            var totalStations = await _context.MapStations.CountAsync();
            var totalCustomers = await _context.Users.CountAsync();

            var vm = new HomeViewModel
            {
                Setting = setting,
                DockFeatures = dockFeatures,
                MainServices = mainServices,
                PackageTypes = packageTypes,
                PriceHighlights = priceHighlights,
                Stations = stations,
                Reviews = reviews,
                TotalOrders = totalOrders,
                TotalDeliveredOrders = totalDelivered,
                TotalStations = totalStations,
                TotalCustomers = totalCustomers
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Track(string? orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return View(null);
            }

            orderCode = orderCode.Trim();

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .Include(o => o.OrderStatusHistories)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            ViewBag.SearchCode = orderCode;
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> EstimatePrice(string deliveryMethod, decimal weight, decimal distance)
        {
            if (string.IsNullOrWhiteSpace(deliveryMethod) || weight <= 0 || distance <= 0)
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin hợp lệ." });
            }

            var priceRow = await _context.PriceTables
                .Where(p =>
                    p.DeliveryMethod == deliveryMethod &&
                    p.MinWeight < weight && weight <= p.MaxWeight &&
                    p.MinDistance < distance && distance <= p.MaxDistance)
                .OrderBy(p => p.MinWeight)
                .ThenBy(p => p.MinDistance)
                .FirstOrDefaultAsync();

            if (priceRow != null)
            {
                return Json(new
                {
                    success = true,
                    price = priceRow.Price,
                    weightRange = priceRow.WeightRange,
                    distanceRange = priceRow.DistanceRange
                });
            }

            return Json(new
            {
                success = false,
                message = "Không tìm thấy mức giá phù hợp cho khối lượng và khoảng cách này (Vui lòng liên hệ tổng đài để được báo giá)."
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
