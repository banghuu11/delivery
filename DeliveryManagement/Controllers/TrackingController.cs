using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DeliveryManagement.Controllers
{
    /// <summary>
    /// Bản đồ vị trí shipper trực tiếp.
    /// Người được xem: Admin, nhân viên kho, khách hàng chủ đơn
    /// và chính shipper được gán đơn.
    /// </summary>
    [Authorize]
    public class TrackingController : Controller
    {
        private const string DeliveringStatus = "Đã nhận – Đang giao";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRouteDistanceService _routeDistanceService;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        public TrackingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IRouteDistanceService routeDistanceService,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _routeDistanceService = routeDistanceService;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
        }

        private bool CanView(DeliveryOrder order, string userId)
        {
            return User.IsInRole("Admin")
                || User.IsInRole("WarehouseStaff")
                || order.CustomerId == userId
                || (User.IsInRole("DeliveryStaff")
                    && order.DeliveryStaffId == userId);
        }

        // Trang tổng quan: tất cả shipper đang giao trên một bản đồ (chỉ nhân viên kho).
        [Authorize(Roles = "WarehouseStaff")]
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "WarehouseStaff")]
        [HttpGet]
        public async Task<IActionResult> Active()
        {
            var orders = await _context.DeliveryOrders
                .AsNoTracking()
                .Where(o => o.CurrentStatus == DeliveringStatus)
                .Select(o => new
                {
                    o.OrderCode,
                    o.ReceiverAddress,
                    ShipperName = o.DeliveryStaff != null
                        ? o.DeliveryStaff.FullName
                        : null
                })
                .ToListAsync();

            var codes = orders.Select(o => o.OrderCode).ToList();

            var locations = await _context.ShipperLocations
                .AsNoTracking()
                .Where(l => codes.Contains(l.OrderCode))
                .ToListAsync();

            var now = DateTime.Now;

            var items = orders.Select(o =>
            {
                var loc = locations.FirstOrDefault(l => l.OrderCode == o.OrderCode);

                return new
                {
                    orderCode = o.OrderCode,
                    receiverAddress = o.ReceiverAddress,
                    shipperName = string.IsNullOrWhiteSpace(o.ShipperName)
                        ? "Shipper"
                        : o.ShipperName,
                    hasLocation = loc != null,
                    latitude = loc?.Latitude,
                    longitude = loc?.Longitude,
                    secondsAgo = loc == null
                        ? (int?)null
                        : (int)Math.Max(0, (now - loc.UpdatedAt).TotalSeconds)
                };
            }).ToList();

            return Json(new { success = true, items });
        }

        // Trang bản đồ của một đơn.
        [HttpGet]
        public async Task<IActionResult> Map(string? orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User) ?? string.Empty;
            var code = orderCode.Trim();

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == code);

            if (order == null || !CanView(order, userId))
            {
                return NotFound();
            }

            ViewBag.OrderCode = order.OrderCode;
            ViewBag.Status = order.CurrentStatus;
            return View();
        }

        // Vị trí mới nhất của shipper cho một đơn (client gọi lặp lại mỗi vài giây).
        [HttpGet]
        public async Task<IActionResult> Location(string? orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return BadRequest(new { success = false });
            }

            var userId = _userManager.GetUserId(User) ?? string.Empty;
            var code = orderCode.Trim();

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .Include(o => o.DeliveryStaff)
                .FirstOrDefaultAsync(o => o.OrderCode == code);

            if (order == null || !CanView(order, userId))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Bạn không có quyền xem vị trí của đơn này."
                });
            }

            // Chỉ chia sẻ vị trí khi đơn đang giao.
            if (order.CurrentStatus != DeliveringStatus)
            {
                return Json(new
                {
                    success = true,
                    active = false,
                    status = order.CurrentStatus
                });
            }

            var loc = await _context.ShipperLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.OrderCode == code);

            if (loc == null)
            {
                return Json(new
                {
                    success = true,
                    active = true,
                    hasLocation = false,
                    status = order.CurrentStatus
                });
            }

            var shipperName = string.IsNullOrWhiteSpace(order.DeliveryStaff?.FullName)
                ? "Shipper"
                : order.DeliveryStaff!.FullName;

            return Json(new
            {
                success = true,
                active = true,
                hasLocation = true,
                status = order.CurrentStatus,
                latitude = loc.Latitude,
                longitude = loc.Longitude,
                accuracy = loc.Accuracy,
                secondsAgo = (int)Math.Max(0, (DateTime.Now - loc.UpdatedAt).TotalSeconds),
                shipperName
            });
        }

        // Điểm lấy hàng và điểm giao (chuyển từ địa chỉ thành tọa độ, có cache).
        [HttpGet]
        public async Task<IActionResult> Points(string? orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return BadRequest(new { success = false });
            }

            var userId = _userManager.GetUserId(User) ?? string.Empty;
            var code = orderCode.Trim();

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == code);

            if (order == null || !CanView(order, userId))
            {
                return StatusCode(403, new { success = false });
            }

            var pickup = await GetPointAsync("pickup", code, order.SenderAddress);
            var destination = await GetPointAsync("dest", code, order.ReceiverAddress);

            return Json(new
            {
                success = true,
                pickup = pickup == null ? null : new
                {
                    latitude = pickup.Value.Latitude,
                    longitude = pickup.Value.Longitude,
                    address = order.SenderAddress
                },
                destination = destination == null ? null : new
                {
                    latitude = destination.Value.Latitude,
                    longitude = destination.Value.Longitude,
                    address = order.ReceiverAddress
                }
            });
        }

        // Đường đi từ vị trí hiện tại của shipper đến điểm giao (OSRM, đường bộ).
        // Kết quả được nhớ vài giây cho mỗi đơn để nhiều người xem cùng lúc
        // chỉ tốn một lần gọi dịch vụ định tuyến.
        [HttpGet]
        public async Task<IActionResult> RouteToDestination(string? orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return BadRequest(new { success = false });
            }

            var userId = _userManager.GetUserId(User) ?? string.Empty;
            var code = orderCode.Trim();

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == code);

            if (order == null || !CanView(order, userId))
            {
                return StatusCode(403, new { success = false });
            }

            if (order.CurrentStatus != DeliveringStatus)
            {
                return Json(new { success = false });
            }

            var cacheKey = $"track_route_{code}";

            if (_cache.TryGetValue(cacheKey, out object? cachedPayload) &&
                cachedPayload != null)
            {
                return Json(cachedPayload);
            }

            var loc = await _context.ShipperLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.OrderCode == code);

            if (loc == null)
            {
                return Json(new { success = false });
            }

            var destination = await GetPointAsync("dest", code, order.ReceiverAddress);

            if (destination == null)
            {
                return Json(new { success = false });
            }

            try
            {
                var url = FormattableString.Invariant(
                    $"https://router.project-osrm.org/route/v1/driving/{loc.Longitude},{loc.Latitude};{destination.Value.Longitude},{destination.Value.Latitude}?overview=full&geometries=geojson");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("DeliveryManagement/1.0");

                using var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false });
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                if (root.GetProperty("code").GetString() != "Ok")
                {
                    return Json(new { success = false });
                }

                var route = root.GetProperty("routes")[0];
                var distanceMeters = route.GetProperty("distance").GetDouble();
                var durationSeconds = route.GetProperty("duration").GetDouble();

                var all = new List<double[]>();

                foreach (var c in route.GetProperty("geometry")
                             .GetProperty("coordinates").EnumerateArray())
                {
                    // GeoJSON là [kinh độ, vĩ độ]; Leaflet cần [vĩ độ, kinh độ].
                    all.Add(new[] { c[1].GetDouble(), c[0].GetDouble() });
                }

                if (all.Count < 2)
                {
                    return Json(new { success = false });
                }

                // Rút bớt điểm nếu tuyến quá dài để giảm dung lượng trả về.
                var points = all;

                if (all.Count > 1500)
                {
                    var step = (int)Math.Ceiling(all.Count / 1500.0);
                    points = all.Where((_, i) => i % step == 0).ToList();

                    if (points[points.Count - 1] != all[all.Count - 1])
                    {
                        points.Add(all[all.Count - 1]);
                    }
                }

                var payload = new
                {
                    success = true,
                    distanceKm = Math.Round(distanceMeters / 1000.0, 2),
                    durationMin = (int)Math.Max(1, Math.Round(durationSeconds / 60.0)),
                    points
                };

                _cache.Set(cacheKey, (object)payload, TimeSpan.FromSeconds(12));

                return Json(payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[TRACKING] Lỗi tính đường đi đơn {code}: {ex.Message}");

                return Json(new { success = false });
            }
        }

        private async Task<(double Latitude, double Longitude)?> GetPointAsync(
            string kind,
            string orderCode,
            string address)
        {
            var key = $"track_point_{kind}_{orderCode}";

            if (_cache.TryGetValue(key, out (double Latitude, double Longitude) cached))
            {
                return cached;
            }

            var point = await _routeDistanceService.GeocodeAddressAsync(address);

            if (point.HasValue)
            {
                _cache.Set(key, point.Value, TimeSpan.FromHours(12));
            }

            return point;
        }
    }
}
