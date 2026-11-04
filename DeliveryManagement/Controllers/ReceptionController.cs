
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
        private const string ReadyStatus = "Đã nhận - Chưa giao";
        private const string CanceledStatus = "Đã hủy";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReceptionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // DANH SÁCH ĐƠN CẦN TIẾP NHẬN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .Where(o => o.CurrentStatus != CanceledStatus)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // ==========================================
        // MỞ TRANG GHI NHẬN
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Record(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
                return NotFound();

            if (order.CurrentStatus == CanceledStatus)
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
                        PackageTypeName =
                            i.PackageType?.TypeName ?? "Chưa phân loại",
                        Description = i.Description ?? string.Empty,
                        Quantity = i.Quantity,
                        Weight = i.Weight,
                        IsFragile = i.IsFragile,
                        IsValuable = i.IsValuable
                    })
                    .ToList()
            };

            // Nếu đã tiếp nhận thì chỉ được xem hoặc in đơn hàng.
            ViewBag.IsReadOnly = order.ReceivedAt.HasValue;
            ViewBag.ReceivedAt = order.ReceivedAt;
            ViewBag.ReceivedBy = order.ReceivedBy;

            return View(model);
        }

        // ==========================================
        // XÁC NHẬN TIẾP NHẬN ĐƠN HÀNG
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Record(ReceptionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderCode == model.OrderCode);

            if (order == null)
                return NotFound();

            // Không cho phép tiếp nhận đơn đã hủy.
            if (order.CurrentStatus == CanceledStatus)
            {
                TempData["ErrorMessage"] =
                    "Không thể ghi nhận đơn hàng đã hủy.";

                return RedirectToAction(nameof(Index));
            }

            // Không cho phép ghi nhận lại đơn đã tiếp nhận.
            if (order.ReceivedAt.HasValue)
            {
                TempData["ErrorMessage"] =
                    "Đơn hàng đã được ghi nhận. Chỉ có thể xem hoặc in đơn hàng.";

                return RedirectToAction(
                    nameof(Record),
                    new { id = order.OrderCode });
            }

            // Kiểm tra dữ liệu đầu vào.
            if (!ModelState.IsValid)
            {
                var invalidModel = model;
                invalidModel.Items ??= new List<ReceptionItemViewModel>();

                var savedOrder = await _context.DeliveryOrders
                    .Include(o => o.OrderItems)
                        .ThenInclude(i => i.PackageType)
                    .FirstOrDefaultAsync(o => o.OrderCode == model.OrderCode);

                if (savedOrder != null)
                {
                    foreach (var item in invalidModel.Items)
                    {
                        var savedItem = savedOrder.OrderItems
                            .FirstOrDefault(
                                i => i.OrderItemId == item.OrderItemId);

                        if (savedItem != null)
                        {
                            item.PackageTypeName =
                                savedItem.PackageType?.TypeName
                                ?? "Chưa phân loại";
                        }
                    }
                }

                ViewBag.IsReadOnly = false;

                return View(invalidModel);
            }

            if (model.Items == null || model.Items.Count == 0)
            {
                ModelState.AddModelError(
                    "",
                    "Đơn hàng chưa có thông tin hàng hóa.");

                ViewBag.IsReadOnly = false;

                return View(model);
            }

            // ==========================================
            // 1. CẬP NHẬT THÔNG TIN NGƯỜI NHẬN
            // ==========================================

            order.ReceiverName = model.ReceiverName;
            order.ReceiverPhone = model.ReceiverPhone;
            order.ReceiverAddress = model.ReceiverAddress;

            // ==========================================
            // 2. CẬP NHẬT THÔNG TIN HÀNG HÓA
            // ==========================================

            // Chỉ cập nhật những mặt hàng thực sự thuộc đơn này.
            foreach (var itemModel in model.Items)
            {
                var item = order.OrderItems.FirstOrDefault(
                    i => i.OrderItemId == itemModel.OrderItemId);

                if (item == null)
                    continue;

                item.Description = itemModel.Description ?? string.Empty;
                item.Quantity = itemModel.Quantity;
                item.Weight = itemModel.Weight;
                item.IsFragile = itemModel.IsFragile;
                item.IsValuable = itemModel.IsValuable;
            }

            // ==========================================
            // 3. CẬP NHẬT THÔNG TIN TIẾP NHẬN
            // ==========================================

            order.ReceivedBy = user.Id;
            order.ReceivedAt = DateTime.Now;
            order.ReceptionNote = model.ReceptionNote;

            // QUAN TRỌNG:
            // Chuyển trạng thái sang Đã nhận - Chưa giao.
            // Đây là trạng thái mà WarehouseController
            // đang sử dụng để lọc đơn chờ xử lý.
            order.CurrentStatus = ReadyStatus;

            // ==========================================
            // 4. LƯU LỊCH SỬ THAY ĐỔI TRẠNG THÁI
            // ==========================================

            _context.OrderStatusHistories.Add(
                new OrderStatusHistory
                {
                    OrderCode = order.OrderCode,
                    Status = ReadyStatus,
                    ChangedBy = user.Id,
                    ChangedAt = DateTime.Now
                });

            // ==========================================
            // 5. LƯU DỮ LIỆU
            // ==========================================

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine(
                    "[RECEPTION] Lỗi khi lưu thông tin tiếp nhận:");

                Console.WriteLine(ex.ToString());

                TempData["ErrorMessage"] =
                    "Có lỗi xảy ra khi lưu thông tin tiếp nhận. Vui lòng thử lại.";

                return RedirectToAction(
                    nameof(Record),
                    new { id = order.OrderCode });
            }

            TempData["SuccessMessage"] =
                $"Đã tiếp nhận đơn {order.OrderCode} thành công. " +
                $"Trạng thái: {ReadyStatus}.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 6. CHAT HỖ TRỢ KHÁCH HÀNG (STORY 11.0)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ChatSupport(int? id)
        {
            var conversations = await _context.Conversations
                .Include(c => c.Customer)
                .Include(c => c.Staff)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            Conversation? activeConversation = null;
            if (id.HasValue && id.Value > 0)
            {
                activeConversation = conversations.FirstOrDefault(c => c.ConversationId == id.Value);
            }

            if (activeConversation == null && conversations.Any())
            {
                activeConversation = conversations.First();
            }

            ViewBag.ActiveConversation = activeConversation;
            return View(conversations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StaffReply(int conversationId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung phản hồi không được để trống.";
                return RedirectToAction(nameof(ChatSupport), new { id = conversationId });
            }

            var conv = await _context.Conversations.FindAsync(conversationId);
            if (conv == null)
            {
                return NotFound();
            }

            var staff = await _userManager.GetUserAsync(User);
            if (staff == null)
            {
                return Unauthorized();
            }

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = staff.Id,
                Content = content.Trim(),
                SentAt = DateTime.Now
            };

            conv.StaffId = staff.Id;
            conv.Status = "StaffAnswered";

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã gửi tin nhắn phản hồi tới khách hàng thành công.";
            return RedirectToAction(nameof(ChatSupport), new { id = conversationId });
        }
    }
}
