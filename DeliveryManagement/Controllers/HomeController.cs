using System.Diagnostics;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
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
            ViewBag.PackageTypes = await _context.PackageTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.TypeName)
                .ToListAsync();

            ViewBag.TotalCompletedOrders = await _context.DeliveryOrders
                .CountAsync(o => o.CurrentStatus == "Đã giao" || o.CurrentStatus == "Giao thành công");

            ViewBag.TotalOrders = await _context.DeliveryOrders.CountAsync();

            return View();
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
