using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private const string StaffRoles =
            "Admin,ReceptionStaff,WarehouseStaff,DeliveryStaff";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // KHÁCH HÀNG: MỞ TRANG ĐÁNH GIÁ
        // ==========================================

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public async Task<IActionResult> Create(string? orderCode)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(orderCode))
                return NotFound();

            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == orderCode &&
                    o.CustomerId == user.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng hoặc bạn không có quyền đánh giá đơn này.";

                return RedirectToAction("Index", "DeliveryOrder");
            }

            if (order.CurrentStatus != "Đã Giao")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể đánh giá đơn hàng đã giao thành công.";

                return RedirectToAction(
                    "Details",
                    "DeliveryOrder",
                    new { id = orderCode });
            }

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.OrderCode == orderCode);

            if (alreadyReviewed)
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng này đã được đánh giá trước đó.";

                return RedirectToAction(
                    "Details",
                    "DeliveryOrder",
                    new { id = orderCode });
            }

            ViewBag.OrderCode = orderCode;
            ViewBag.Order = order;

            return View(new Review
            {
                OrderCode = orderCode,
                CustomerId = user.Id
            });
        }

        // ==========================================
        // KHÁCH HÀNG: GỬI ĐÁNH GIÁ
        // ==========================================

        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string? orderCode,
            int rating,
            string? comment)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(orderCode))
                return NotFound();

            var order = await _context.DeliveryOrders
                .FirstOrDefaultAsync(o =>
                    o.OrderCode == orderCode &&
                    o.CustomerId == user.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đơn hàng hoặc bạn không có quyền đánh giá đơn này.";

                return RedirectToAction("Index", "DeliveryOrder");
            }

            if (order.CurrentStatus != "Đã Giao")
            {
                TempData["ErrorMessage"] =
                    "Chỉ có thể đánh giá đơn hàng đã giao thành công.";

                return RedirectToAction(
                    "Details",
                    "DeliveryOrder",
                    new { id = orderCode });
            }

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.OrderCode == orderCode);

            if (alreadyReviewed)
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng này đã được đánh giá trước đó.";

                return RedirectToAction(
                    "Details",
                    "DeliveryOrder",
                    new { id = orderCode });
            }

            // Kiểm tra số sao.
            if (rating < 1 || rating > 5)
            {
                ModelState.AddModelError(
                    "Rating",
                    "Vui lòng chọn số sao từ 1 đến 5.");
            }

            // Kiểm tra nội dung nhận xét.
            if (string.IsNullOrWhiteSpace(comment))
            {
                ModelState.AddModelError(
                    "Comment",
                    "Vui lòng nhập nhận xét.");
            }
            else if (comment.Trim().Length > 1000)
            {
                ModelState.AddModelError(
                    "Comment",
                    "Nhận xét không được vượt quá 1000 ký tự.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.OrderCode = orderCode;
                ViewBag.Order = order;

                return View(new Review
                {
                    OrderCode = orderCode,
                    CustomerId = user.Id,
                    Rating = rating,
                    Comment = comment
                });
            }

            var review = new Review
            {
                OrderCode = orderCode,
                CustomerId = user.Id,
                Rating = rating,
                Comment = comment!.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Cảm ơn bạn đã gửi đánh giá cho đơn hàng!";
            }
            catch (DbUpdateException)
            {
                // Phòng trường hợp hai yêu cầu đánh giá được gửi gần như cùng lúc.
                _context.Entry(review).State = EntityState.Detached;

                TempData["ErrorMessage"] =
                    "Đơn hàng này đã được đánh giá hoặc không thể lưu đánh giá.";
            }

            return RedirectToAction(
                "Details",
                "DeliveryOrder",
                new { id = orderCode });
        }

        // ==========================================
        // NHÂN VIÊN / ADMIN: QUẢN LÝ ĐÁNH GIÁ
        // ==========================================

        [Authorize(Roles = StaffRoles)]
        [HttpGet]
        public async Task<IActionResult> Index(
            int? rating,
            string? responseStatus,
            string? keyword)
        {
            // Chuẩn hóa bộ lọc số sao.
            if (rating.HasValue &&
                (rating.Value < 1 || rating.Value > 5))
            {
                rating = null;
            }

            // Chuẩn hóa trạng thái phản hồi.
            if (responseStatus != "pending" &&
                responseStatus != "replied")
            {
                responseStatus = "all";
            }

            keyword = keyword?.Trim();

            // Thống kê tổng quan trên toàn bộ đánh giá.
            var allReviews = _context.Reviews.AsNoTracking();

            ViewBag.TotalReviews =
                await allReviews.CountAsync();

            ViewBag.PendingReviews =
                await allReviews.CountAsync(r =>
                    r.Response == null || r.Response == "");

            ViewBag.RespondedReviews =
                await allReviews.CountAsync(r =>
                    r.Response != null && r.Response != "");

            var averageRating = await allReviews
                .Select(r => (double?)r.Rating)
                .AverageAsync();

            ViewBag.AverageRating = averageRating ?? 0;

            // Truy vấn danh sách đánh giá.
            var query = _context.Reviews
                .AsNoTracking()
                .Include(r => r.Customer)
                .Include(r => r.DeliveryOrder)
                .Include(r => r.Staff)
                .AsQueryable();

            // Lọc theo số sao.
            if (rating.HasValue)
            {
                query = query.Where(r =>
                    r.Rating == rating.Value);
            }

            // Lọc theo trạng thái phản hồi.
            if (responseStatus == "pending")
            {
                query = query.Where(r =>
                    r.Response == null || r.Response == "");
            }
            else if (responseStatus == "replied")
            {
                query = query.Where(r =>
                    r.Response != null && r.Response != "");
            }

            // Tìm theo mã đơn, tên hoặc email khách hàng.
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(r =>
                    r.OrderCode.Contains(keyword) ||
                    (r.Customer != null &&
                        (
                            r.Customer.FullName.Contains(keyword) ||
                            (r.Customer.Email != null &&
                             r.Customer.Email.Contains(keyword))
                        )));
            }

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.SelectedRating = rating;
            ViewBag.SelectedResponseStatus = responseStatus;
            ViewBag.Keyword = keyword;

            return View(reviews);
        }

        // ==========================================
        // NHÂN VIÊN / ADMIN: GỬI HOẶC CẬP NHẬT PHẢN HỒI
        // ==========================================

        [Authorize(Roles = StaffRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(
            int reviewId,
            string? response,
            int? rating,
            string? responseStatus,
            string? keyword)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r =>
                    r.ReviewId == reviewId);

            if (review == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy đánh giá cần phản hồi.";

                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng nhập nội dung phản hồi.";
            }
            else if (response.Trim().Length > 1000)
            {
                TempData["ErrorMessage"] =
                    "Phản hồi không được vượt quá 1000 ký tự.";
            }
            else
            {
                review.Response = response.Trim();
                review.RespondedBy = user.Id;
                review.RespondedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Đã lưu phản hồi đánh giá thành công.";
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    rating,
                    responseStatus,
                    keyword
                });
        }
    }
}