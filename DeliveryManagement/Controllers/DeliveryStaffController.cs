using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "DeliveryStaff")]
    public class DeliveryStaffController : Controller
    {
        private const string ReadyStatus = "Đã nhận - Chưa giao";
        private const string DeliveringStatus = "Đã nhận – Đang giao";
        private const string DeliveredStatus = "Đã Giao";
        private const string FailedStatus = "Giao thất bại - Chờ xử lý";
        private const string CanceledStatus = "Đã hủy";

        // Các giá trị hình thức giao hàng thường được hệ thống hiện tại sử dụng.
        // Đơn thường do shipper tự nhận; đơn nhanh do kho phân công.
        private static readonly string[] NormalDeliveryMethods =
        {
            "Thường",
            "Giao tiêu chuẩn",
            "Giao tiết kiệm"
        };

        private const long MaxProofSize = 2 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IPaymentService _paymentService;

        public DeliveryStaffController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IPaymentService paymentService)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _paymentService = paymentService;
        }

        // ==========================================
        // DANH SÁCH ĐƠN CỦA SHIPPER
        // - Đơn đã được kho phân công cho tài khoản hiện tại.
        // - Đơn giao thường đã tiếp nhận, đang chờ một shipper nhận.
        // - Không hiển thị đơn giao nhanh chưa được kho phân công.
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var myOrders = await _context.DeliveryOrders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .Where(o =>
                    o.CurrentStatus != CanceledStatus &&
                    (
                        // Đơn đã được kho phân công cho shipper hiện tại.
                        o.DeliveryStaffId == user.Id
                        ||
                        // Đơn giao thường đã tiếp nhận và chưa có shipper.
                        (
                            o.ReceivedAt.HasValue &&
                            NormalDeliveryMethods.Contains(o.DeliveryMethod) &&
                            o.CurrentStatus == ReadyStatus &&
                            o.DeliveryStaffId == null
                        )
                    ))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // View có thể sử dụng Model hoặc ViewBag.MyOrders.
            ViewBag.MyOrders = myOrders;

            return View(myOrders);
        }

        // ==========================================
        // CHI TIẾT ĐƠN GIAO HÀNG
        // Chỉ shipper đã được gán đơn mới được xem.
        // Đơn thường chỉ được xem sau khi nhận giao thành công.
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
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id);

            // Không cho shipper xem đơn được phân công cho người khác
            // hoặc đơn chưa được ai nhận.
            if (order == null)
            {
                return NotFound();
            }

            // Lịch sử check-in vị trí của đơn hàng (mới nhất trước).
            ViewBag.CheckIns = await _context.CheckIns
                .AsNoTracking()
                .Include(ci => ci.MapStation)
                .Where(ci => ci.OrderCode == id)
                .OrderByDescending(ci => ci.CheckInTime)
                .ToListAsync();

            return View(order);
        }

        // ==========================================
        // SHIPPER TỰ NHẬN ĐƠN GIAO THƯỜNG
        // Chỉ nhận đơn đã tiếp nhận, đúng hình thức giao thường,
        // đang ở trạng thái Ready và chưa có shipper.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // ExecuteUpdateAsync kiểm tra và cập nhật trong cùng một câu lệnh.
                // Nếu shipper khác nhận trước, affectedRows sẽ bằng 0.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.ReceivedAt.HasValue &&
                        NormalDeliveryMethods.Contains(o.DeliveryMethod) &&
                        o.CurrentStatus == ReadyStatus &&
                        o.DeliveryStaffId == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.DeliveryStaffId, user.Id)
                        .SetProperty(o => o.CurrentStatus, DeliveringStatus));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể nhận đơn. Đơn chưa được tiếp nhận, " +
                        "không phải đơn giao thường, đã được shipper khác nhận " +
                        "hoặc trạng thái đơn đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderCode = id,
                    Status = DeliveringStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Bạn đã nhận giao đơn {id} thành công. " +
                    $"Trạng thái đơn hàng: {DeliveringStatus}.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                Console.WriteLine(
                    $"[DELIVERY STAFF] Lỗi khi nhận đơn {id}: {ex}");

                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi nhận đơn hàng. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // XÁC NHẬN BẮT ĐẦU GIAO HÀNG
        // Đơn được chuyển sang đang giao ngay khi shipper nhận
        // đơn thường hoặc khi kho phân công đơn giao nhanh.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartDelivery(string id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var orderExists = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (!orderExists)
            {
                TempData["ErrorMessage"] =
                    "Đơn không thuộc quyền xử lý của bạn hoặc chưa được phân công/nhận giao.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"Đơn {id} đang ở trạng thái đang giao. " +
                "Bạn có thể cập nhật kết quả giao hàng.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // XÁC NHẬN GIAO HÀNG THÀNH CÔNG KÈM ẢNH
        // Chỉ shipper được phân công và đơn đang giao mới được xác nhận.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<IActionResult> CompleteDelivery(
            string id,
            IFormFile? proofImage,
            string? deliveryNote)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var orderExists = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (!orderExists)
            {
                TempData["ErrorMessage"] =
                    "Đơn không tồn tại, không thuộc quyền xử lý của bạn " +
                    "hoặc không ở trạng thái đang giao.";

                return RedirectToAction(nameof(Index));
            }

            // Bắt buộc có ảnh minh chứng.
            if (proofImage == null || proofImage.Length == 0)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng tải ảnh minh chứng giao hàng.";

                return RedirectToAction(nameof(Index));
            }

            if (proofImage.Length > MaxProofSize)
            {
                TempData["ErrorMessage"] =
                    "Ảnh minh chứng không được vượt quá 2 MB.";

                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(proofImage.FileName);

            if ((!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                 !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) ||
                !string.Equals(
                    proofImage.ContentType,
                    "image/jpeg",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Chỉ chấp nhận ảnh JPG/JPEG.";

                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra chữ ký JPEG cơ bản trước khi lưu file.
            await using (var input = proofImage.OpenReadStream())
            {
                var signature = new byte[3];

                var bytesRead = await input.ReadAsync(
                    signature, 0, signature.Length);

                if (bytesRead != 3 ||
                    signature[0] != 0xFF ||
                    signature[1] != 0xD8 ||
                    signature[2] != 0xFF)
                {
                    TempData["ErrorMessage"] =
                        "File tải lên không phải ảnh JPEG hợp lệ.";

                    return RedirectToAction(nameof(Index));
                }
            }

            deliveryNote = deliveryNote?.Trim();

            if (deliveryNote?.Length > 500)
            {
                TempData["ErrorMessage"] =
                    "Ghi chú không được vượt quá 500 ký tự.";

                return RedirectToAction(nameof(Index));
            }

            var webRoot = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");
            }

            var uploadsFolder = Path.Combine(
                webRoot,
                "uploads",
                "delivery-proofs");

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var physicalPath = Path.Combine(uploadsFolder, fileName);
            var relativePath = $"/uploads/delivery-proofs/{fileName}";

            try
            {
                // Lưu ảnh minh chứng.
                await using (var output = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await proofImage.CopyToAsync(output);
                }

                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                // Kiểm tra lại quyền sở hữu và trạng thái tại thời điểm cập nhật.
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == DeliveringStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            o => o.CurrentStatus,
                            DeliveredStatus)
                        .SetProperty(
                            o => o.DeliveryResult,
                            "Giao thành công")
                        .SetProperty(
                            o => o.ProofImage,
                            relativePath)
                        .SetProperty(
                            o => o.DeliveryNote,
                            deliveryNote));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();
                    TryDeleteFile(physicalPath);

                    TempData["ErrorMessage"] =
                        "Không thể xác nhận. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
                    {
                        OrderCode = id,
                        Status = DeliveredStatus,
                        ChangedBy = user.Id,
                        ChangedAt = DateTime.Now
                    });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                TryDeleteFile(physicalPath);

                Console.WriteLine(
                    $"[DELIVERY STAFF] Lỗi hoàn tất đơn {id}: {ex}");

                TempData["ErrorMessage"] =
                    "Có lỗi khi lưu kết quả giao hàng. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            // Thu tiền COD sau khi kết quả giao thành công đã được lưu.
            await _paymentService.CollectCodAsync(id);

            TempData["SuccessMessage"] =
                $"Đã ghi nhận giao hàng thành công cho đơn {id}.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // GHI NHẬN GIAO HÀNG THẤT BẠI
        // Sau khi thất bại, kho xử lý giao lại hoặc hoàn hàng.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FailDelivery(
            string id,
            string failureReason,
            string? deliveryNote)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var validReasons = new[]
            {
                "Không liên lạc được với người nhận",
                "Sai địa chỉ",
                "Khách từ chối nhận",
                "Lý do khác"
            };

            if (!validReasons.Contains(failureReason))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng chọn lý do giao thất bại hợp lệ.";

                return RedirectToAction(nameof(Index));
            }

            deliveryNote = deliveryNote?.Trim();

            if (deliveryNote?.Length > 500)
            {
                TempData["ErrorMessage"] =
                    "Ghi chú không được vượt quá 500 ký tự.";

                return RedirectToAction(nameof(Index));
            }

            if (failureReason == "Lý do khác" &&
                string.IsNullOrWhiteSpace(deliveryNote))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng nhập ghi chú khi chọn lý do khác.";

                return RedirectToAction(nameof(Index));
            }

            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == id &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Đơn không tồn tại, không thuộc quyền xử lý của bạn " +
                    "hoặc không ở trạng thái đang giao.";

                return RedirectToAction(nameof(Index));
            }

            var failureDetail =
                $"Lý do giao thất bại: {failureReason}";

            if (!string.IsNullOrWhiteSpace(deliveryNote))
            {
                failureDetail += $". Ghi chú: {deliveryNote}";
            }

            var previousNote = order.DeliveryNote?.Trim();

            var noteToSave = string.IsNullOrWhiteSpace(previousNote)
                ? failureDetail
                : $"{previousNote}{Environment.NewLine}{failureDetail}";

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var affectedRows = await _context.DeliveryOrders
                    .Where(o =>
                        o.OrderCode == id &&
                        o.DeliveryStaffId == user.Id &&
                        o.CurrentStatus == DeliveringStatus)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            o => o.CurrentStatus,
                            FailedStatus)
                        .SetProperty(
                            o => o.DeliveryResult,
                            "Giao thất bại")
                        .SetProperty(
                            o => o.DeliveryNote,
                            noteToSave));

                if (affectedRows != 1)
                {
                    await transaction.RollbackAsync();

                    TempData["ErrorMessage"] =
                        "Không thể cập nhật. Trạng thái đơn hàng đã thay đổi.";

                    return RedirectToAction(nameof(Index));
                }

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
                    {
                        OrderCode = order.OrderCode,
                        Status = FailedStatus,
                        ChangedBy = user.Id,
                        ChangedAt = DateTime.Now
                    });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                Console.WriteLine(
                    $"[DELIVERY STAFF] Lỗi ghi nhận giao thất bại {id}: {ex}");

                TempData["ErrorMessage"] =
                    "Có lỗi khi lưu kết quả giao thất bại. Vui lòng thử lại.";

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"Đã ghi nhận giao thất bại cho đơn {order.OrderCode}. " +
                "Đơn đang chờ kho xử lý tiếp.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // TỰ ĐỘNG CHECK-IN VỊ TRÍ TẠI BƯU CỤC
        // Trình duyệt của shipper gửi tọa độ GPS định kỳ.
        // Nếu shipper đang ở trong bán kính CheckInRadiusMeters của một
        // bưu cục đang hoạt động thì ghi nhận một lượt check-in cho đơn.
        // ==========================================
        private const double CheckInRadiusMeters = 150;
        private const double MaxGpsAccuracyMeters = 200;
        private static readonly TimeSpan CheckInCooldown = TimeSpan.FromMinutes(5);

        public class AutoCheckInRequest
        {
            public string? OrderCode { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double? Accuracy { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoCheckIn(
            [FromBody] AutoCheckInRequest request)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            if (request == null ||
                string.IsNullOrWhiteSpace(request.OrderCode) ||
                Math.Abs(request.Latitude) > 90 ||
                Math.Abs(request.Longitude) > 180 ||
                (request.Latitude == 0 && request.Longitude == 0))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu vị trí không hợp lệ."
                });
            }

            // Tín hiệu GPS quá yếu thì bỏ qua, tránh check-in nhầm bưu cục.
            if (request.Accuracy.HasValue &&
                request.Accuracy.Value > MaxGpsAccuracyMeters)
            {
                return Json(new
                {
                    success = true,
                    checkedIn = false,
                    message = "Tín hiệu GPS yếu, chưa thể xác định vị trí."
                });
            }

            // Chỉ shipper được gán đơn, và đơn đang giao, mới được check-in.
            var orderCode = request.OrderCode.Trim();

            var orderIsActive = await _context.DeliveryOrders
                .AnyAsync(o =>
                    o.OrderCode == orderCode &&
                    o.DeliveryStaffId == user.Id &&
                    o.CurrentStatus == DeliveringStatus);

            if (!orderIsActive)
            {
                return Conflict(new
                {
                    success = false,
                    stop = true,
                    message = "Đơn không còn ở trạng thái đang giao nên đã dừng check-in."
                });
            }

            // Lưu vị trí mới nhất để khách, admin và kho xem trên bản đồ.
            try
            {
                var location = await _context.ShipperLocations
                    .FirstOrDefaultAsync(l => l.OrderCode == orderCode);

                if (location == null)
                {
                    location = new ShipperLocation { OrderCode = orderCode };
                    _context.ShipperLocations.Add(location);
                }

                location.DeliveryStaffId = user.Id;
                location.Latitude = request.Latitude;
                location.Longitude = request.Longitude;
                location.Accuracy = request.Accuracy;
                location.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                Console.WriteLine(
                    $"[DELIVERY STAFF] Lỗi lưu vị trí đơn {orderCode}: {ex.Message}");
            }

            var stations = await _context.MapStations
                .AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync();

            var nearest = stations
                .Select(s => new
                {
                    Station = s,
                    Distance = DistanceMeters(
                        request.Latitude,
                        request.Longitude,
                        (double)s.Latitude,
                        (double)s.Longitude)
                })
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (nearest == null)
            {
                return Json(new
                {
                    success = true,
                    checkedIn = false,
                    message = "Chưa có bưu cục nào đang hoạt động."
                });
            }

            if (nearest.Distance > CheckInRadiusMeters)
            {
                return Json(new
                {
                    success = true,
                    checkedIn = false,
                    nearestStation = nearest.Station.StationName,
                    distanceMeters = Math.Round(nearest.Distance)
                });
            }

            // Tránh ghi trùng khi shipper đứng lâu tại một bưu cục:
            // chỉ chặn nếu lượt check-in gần nhất của đơn là ở CHÍNH trạm này
            // và còn trong thời gian chờ. Nếu shipper đã check-in ở trạm khác
            // rồi quay lại thì vẫn ghi lượt mới.
            var lastCheckIn = await _context.CheckIns
                .AsNoTracking()
                .Where(ci =>
                    ci.OrderCode == orderCode &&
                    ci.DeliveryStaffId == user.Id)
                .OrderByDescending(ci => ci.CheckInTime)
                .FirstOrDefaultAsync();

            if (lastCheckIn != null &&
                lastCheckIn.StationId == nearest.Station.StationId &&
                lastCheckIn.CheckInTime >= DateTime.Now - CheckInCooldown)
            {
                return Json(new
                {
                    success = true,
                    checkedIn = false,
                    alreadyCheckedIn = true,
                    stationName = nearest.Station.StationName,
                    lastTime = lastCheckIn.CheckInTime.ToString("HH:mm:ss"),
                    waitMinutes = (int)CheckInCooldown.TotalMinutes
                });
            }

            var checkIn = new CheckIn
            {
                OrderCode = orderCode,
                StationId = nearest.Station.StationId,
                DeliveryStaffId = user.Id,
                Latitude = (decimal)Math.Round(request.Latitude, 7),
                Longitude = (decimal)Math.Round(request.Longitude, 7),
                CheckInTime = DateTime.Now
            };

            try
            {
                _context.CheckIns.Add(checkIn);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[DELIVERY STAFF] Lỗi check-in đơn {orderCode}: {ex}");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Không thể lưu check-in. Hệ thống sẽ thử lại."
                });
            }

            return Json(new
            {
                success = true,
                checkedIn = true,
                stationName = nearest.Station.StationName,
                distanceMeters = Math.Round(nearest.Distance),
                time = checkIn.CheckInTime.ToString("dd/MM/yyyy HH:mm:ss"),
                latitude = request.Latitude,
                longitude = request.Longitude
            });
        }

        // Khoảng cách giữa hai tọa độ (mét), công thức Haversine.
        private static double DistanceMeters(
            double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadius = 6371000;

            static double Rad(double degrees) => degrees * Math.PI / 180;

            var dLat = Rad(lat2 - lat1);
            var dLon = Rad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return 2 * EarthRadius *
                   Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ==========================================
        // XÓA FILE MINH CHỨNG NẾU CẬP NHẬT CSDL THẤT BẠI
        // ==========================================
        private static void TryDeleteFile(string physicalPath)
        {
            try
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch
            {
                // Giữ nguyên lỗi chính nếu xóa file thất bại.
            }
        }
    }
}
