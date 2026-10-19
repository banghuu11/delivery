using System.Diagnostics;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPricingService _pricingService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            IPricingService pricingService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _pricingService = pricingService;
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
        public async Task<IActionResult> GetLiveConditions(double? lat, double? lon)
        {
            var calc = await _pricingService.CalculatePriceAsync("Thường", 1, 1, lat, lon);

            return Json(new
            {
                success = true,
                temperature = calc.CurrentTemperature,
                rainMm = calc.CurrentRainMm,
                weatherDescription = calc.WeatherDescription,
                weatherIcon = calc.WeatherIcon,
                weatherFee = calc.WeatherFee,
                isWeatherSurchargeApplied = calc.IsWeatherSurchargeApplied,

                trafficDescription = calc.TrafficDescription,
                trafficIcon = calc.TrafficIcon,
                trafficFee = calc.TrafficFee,
                isTrafficSurchargeApplied = calc.IsTrafficSurchargeApplied,

                updatedAt = GetVietnamNow().ToString("HH:mm - dd/MM/yyyy")
            });
        }

        [HttpGet]
        public async Task<IActionResult> EstimatePrice(
            string deliveryMethod, 
            decimal weight, 
            decimal distance,
            double? lat = null,
            double? lon = null)
        {
            if (string.IsNullOrWhiteSpace(deliveryMethod) || weight <= 0 || distance <= 0)
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin hợp lệ." });
            }

            var calc = await _pricingService.CalculatePriceAsync(deliveryMethod, weight, distance, lat, lon);

            if (calc.Success)
            {
                return Json(new
                {
                    success = true,
                    price = calc.TotalPrice,
                    baseShippingFee = calc.BaseShippingFee,
                    weightFee = calc.WeightFee,
                    weatherFee = calc.WeatherFee,
                    trafficFee = calc.TrafficFee,
                    weatherDescription = calc.WeatherDescription,
                    trafficDescription = calc.TrafficDescription,
                    weatherIcon = calc.WeatherIcon,
                    trafficIcon = calc.TrafficIcon
                });
            }

            return Json(new
            {
                success = false,
                message = "Không tìm thấy mức giá phù hợp cho khối lượng và khoảng cách này."
            });
        }

        private static DateTime GetVietnamNow()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(7);
                }
            }
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
