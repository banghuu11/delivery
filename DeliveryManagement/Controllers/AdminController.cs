using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ==========================================
        // 1. DASHBOARD
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.DeliveryOrders.ToListAsync();
            var users = await _userManager.Users.ToListAsync();

            int customerCount = 0;
            int staffCount = 0;

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (roles.Contains("Customer"))
                {
                    customerCount++;
                }
                else if (roles.Contains("ReceptionStaff") || roles.Contains("WarehouseStaff") || roles.Contains("DeliveryStaff") || roles.Contains("Admin"))
                {
                    staffCount++;
                }
            }

            var vm = new AdminDashboardViewModel
            {
                TotalOrders = orders.Count,
                TotalRevenue = orders.Where(o => o.CurrentStatus != "Đã hủy").Sum(o => o.TotalAmount),
                PendingReceptionOrders = orders.Count(o => o.CurrentStatus == "Chưa nhận" || o.CurrentStatus == "Chờ tiếp nhận"),
                InWarehouseOrders = orders.Count(o => o.CurrentStatus == "Đã nhận" || o.CurrentStatus == "Đã tiếp nhận" || o.CurrentStatus == "Trong kho" || o.CurrentStatus == "Đang xử lý"),
                DeliveringOrders = orders.Count(o => o.CurrentStatus == "Đang giao"),
                CompletedOrders = orders.Count(o => o.CurrentStatus == "Đã giao" || o.CurrentStatus == "Giao thành công"),
                CancelledOrders = orders.Count(o => o.CurrentStatus == "Đã hủy" || o.CurrentStatus == "Giao thất bại"),
                TotalUsers = users.Count,
                TotalCustomers = customerCount,
                TotalStaff = staffCount,
                RecentOrders = await _context.DeliveryOrders
                    .Include(o => o.Customer)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(8)
                    .ToListAsync(),
                PackageTypes = await _context.PackageTypes.OrderBy(p => p.TypeName).ToListAsync()
            };

            return View(vm);
        }

        // ==========================================
        // 2. QUẢN LÝ TÀI KHOẢN & PHÂN QUYỀN
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Users(string? search, string? role)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.Email != null && u.Email.ToLower().Contains(search)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(search)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            var users = await query.ToListAsync();
            var userList = new List<UserListViewModel>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var mainRole = roles.FirstOrDefault() ?? "Customer";

                if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role))
                {
                    continue;
                }

                userList.Add(new UserListViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber ?? string.Empty,
                    Role = mainRole,
                    IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.Now
                });
            }

            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };

            return View(userList);
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email này đã tồn tại trong hệ thống.");
                ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }

            await _userManager.AddToRoleAsync(user, model.Role);
            TempData["SuccessMessage"] = $"Đã tạo tài khoản {model.Email} thành công với quyền {model.Role}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "Customer"
            };

            ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                ViewBag.AllRoles = new List<string> { "Admin", "ReceptionStaff", "WarehouseStaff", "DeliveryStaff", "Customer" };
                return View(model);
            }

            // Cập nhật vai trò (Role)
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await _userManager.AddToRoleAsync(user, model.Role);

            // Đổi mật khẩu nếu có nhập
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật thông tin tài khoản {user.Email} thành công.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.Email == "admin@delivery.com")
            {
                TempData["ErrorMessage"] = "Không thể khóa tài khoản quản trị viên tối cao!";
                return RedirectToAction(nameof(Users));
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.Now)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["SuccessMessage"] = $"Đã mở khóa tài khoản {user.Email}.";
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.Now.AddYears(100));
                TempData["SuccessMessage"] = $"Đã khóa tài khoản {user.Email}.";
            }

            return RedirectToAction(nameof(Users));
        }

        // ==========================================
        // 3. QUẢN LÝ LOẠI HÀNG HÓA (PACKAGE TYPES)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> PackageTypes()
        {
            var list = await _context.PackageTypes.OrderBy(p => p.PackageTypeId).ToListAsync();
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePackageType(PackageType model)
        {
            if (string.IsNullOrWhiteSpace(model.TypeName))
            {
                TempData["ErrorMessage"] = "Tên loại hàng không được để trống.";
                return RedirectToAction(nameof(PackageTypes));
            }

            model.IsActive = true;
            _context.PackageTypes.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm loại hàng '{model.TypeName}' thành công.";
            return RedirectToAction(nameof(PackageTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPackageType(PackageType model)
        {
            var entity = await _context.PackageTypes.FindAsync(model.PackageTypeId);
            if (entity == null)
            {
                return NotFound();
            }

            entity.TypeName = model.TypeName;
            entity.Description = model.Description;
            entity.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật loại hàng '{entity.TypeName}'.";
            return RedirectToAction(nameof(PackageTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePackageType(int id)
        {
            var entity = await _context.PackageTypes.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            bool inUse = await _context.OrderItems.AnyAsync(i => i.PackageTypeId == id);
            if (inUse)
            {
                entity.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Loại hàng '{entity.TypeName}' đang có trong các đơn hàng, hệ thống đã chuyển sang trạng thái Ngưng hoạt động.";
            }
            else
            {
                _context.PackageTypes.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa loại hàng '{entity.TypeName}'.";
            }

            return RedirectToAction(nameof(PackageTypes));
        }

        // ==========================================
        // 4. QUẢN LÝ TẤT CẢ ĐƠN HÀNG
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Orders(string? search, string? status)
        {
            var query = _context.DeliveryOrders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(o =>
                    o.OrderCode.Contains(search) ||
                    o.SenderName.Contains(search) ||
                    o.SenderPhone.Contains(search) ||
                    o.ReceiverName.Contains(search) ||
                    o.ReceiverPhone.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.CurrentStatus == status);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            ViewBag.Search = search;
            ViewBag.SelectedStatus = status;
            ViewBag.AllStatuses = new List<string>
            {
                "Chưa nhận", "Chờ tiếp nhận", "Đã nhận", "Đã tiếp nhận", "Trong kho", "Đang xử lý", "Đang giao", "Đã giao", "Giao thành công", "Đã hủy", "Giao thất bại"
            };

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var order = await _context.DeliveryOrders
                .Include(o => o.Customer)
                .Include(o => o.DeliveryStaff)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.PackageType)
                .Include(o => o.OrderStatusHistories)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.AllStatuses = new List<string>
            {
                "Chưa nhận", "Chờ tiếp nhận", "Đã nhận", "Đã tiếp nhận", "Trong kho", "Đang xử lý", "Đang giao", "Đã giao", "Giao thành công", "Đã hủy", "Giao thất bại"
            };

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(string id, string newStatus, string? note)
        {
            var order = await _context.DeliveryOrders.FirstOrDefaultAsync(o => o.OrderCode == id);
            if (order == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            order.CurrentStatus = newStatus;

            var history = new OrderStatusHistory
            {
                OrderCode = order.OrderCode,
                Status = newStatus,
                ChangedBy = user?.Id ?? "Admin",
                ChangedAt = DateTime.Now
            };

            _context.OrderStatusHistories.Add(history);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn hàng {order.OrderCode} thành '{newStatus}'.";
            return RedirectToAction(nameof(OrderDetails), new { id = order.OrderCode });
        }

        // ==========================================
        // 5. THÊM, SỬA, XÓA ĐƠN HÀNG (CRUD ORDERS)
        // ==========================================

        private async Task LoadOrderDropdownsAsync()
        {
            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            if (!customers.Any())
            {
                customers = await _userManager.Users.ToListAsync();
            }
            ViewBag.Customers = customers;

            var deliveryStaffs = await _userManager.GetUsersInRoleAsync("DeliveryStaff");
            ViewBag.DeliveryStaffs = deliveryStaffs;

            ViewBag.PackageTypes = await _context.PackageTypes.Where(p => p.IsActive).ToListAsync();

            ViewBag.AllStatuses = new List<string>
            {
                "Chờ tiếp nhận", "Đã tiếp nhận", "Trong kho", "Đang xử lý", "Đang giao", "Giao thành công", "Đã hủy", "Giao thất bại"
            };

            ViewBag.DeliveryMethods = new List<string>
            {
                "Giao tiêu chuẩn", "Giao hỏa tốc", "Giao tiết kiệm", "Giao siêu tốc 2h"
            };

            ViewBag.PaymentMethods = new List<string>
            {
                "Tiền mặt khi nhận hàng (COD)", "Chuyển khoản / VNPAY", "Đã thanh toán trước"
            };

            ViewBag.PaymentStatuses = new List<string>
            {
                "Chưa thanh toán", "Đã thanh toán", "Hoàn tiền"
            };
        }

        [HttpGet]
        public async Task<IActionResult> CreateOrder()
        {
            await LoadOrderDropdownsAsync();

            var currentAdmin = await _userManager.GetUserAsync(User);
            var model = new AdminCreateOrderViewModel
            {
                CustomerId = currentAdmin?.Id ?? string.Empty,
                SenderName = currentAdmin?.FullName ?? "Quản trị viên",
                SenderPhone = currentAdmin?.PhoneNumber ?? "19008888",
                SenderAddress = "TP. Hồ Chí Minh",
                DistanceKm = 5,
                TotalAmount = 30000,
                Weight = 1.0m,
                Quantity = 1,
                CurrentStatus = "Chờ tiếp nhận"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(AdminCreateOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadOrderDropdownsAsync();
                return View(model);
            }

            // Sinh mã đơn hàng
            string orderCode = "TD" + DateTime.Now.ToString("yyMMdd") + new Random().Next(1000, 9999);
            while (await _context.DeliveryOrders.AnyAsync(o => o.OrderCode == orderCode))
            {
                orderCode = "TD" + DateTime.Now.ToString("yyMMdd") + new Random().Next(1000, 9999);
            }

            var order = new DeliveryOrder
            {
                OrderCode = orderCode,
                CustomerId = model.CustomerId,
                SenderName = model.SenderName,
                SenderPhone = model.SenderPhone,
                SenderAddress = model.SenderAddress,
                ReceiverName = model.ReceiverName,
                ReceiverPhone = model.ReceiverPhone,
                ReceiverAddress = model.ReceiverAddress,
                DeliveryMethod = model.DeliveryMethod,
                DistanceKm = model.DistanceKm,
                PaymentMethod = model.PaymentMethod,
                PaymentStatus = model.PaymentStatus,
                PaymentAmount = model.TotalAmount,
                PaymentTime = model.PaymentStatus == "Đã thanh toán" ? DateTime.Now : null,
                TotalAmount = model.TotalAmount,
                CurrentStatus = model.CurrentStatus,
                DeliveryStaffId = model.DeliveryStaffId,
                DeliveryNote = model.DeliveryNote,
                CreatedAt = DateTime.Now
            };

            var item = new OrderItem
            {
                OrderCode = orderCode,
                PackageTypeId = model.PackageTypeId,
                Weight = model.Weight,
                Quantity = model.Quantity,
                Description = model.Description ?? "Hàng hóa vận chuyển",
                IsFragile = model.IsFragile,
                IsValuable = model.IsValuable
            };

            order.OrderItems.Add(item);

            var history = new OrderStatusHistory
            {
                OrderCode = orderCode,
                Status = model.CurrentStatus,
                ChangedBy = User.Identity?.Name ?? "Admin",
                ChangedAt = DateTime.Now
            };

            order.OrderStatusHistories.Add(history);

            _context.DeliveryOrders.Add(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo mới đơn hàng #{orderCode} thành công.";
            return RedirectToAction(nameof(Orders));
        }

        [HttpGet]
        public async Task<IActionResult> EditOrder(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                return NotFound();
            }

            await LoadOrderDropdownsAsync();

            var model = new AdminEditOrderViewModel
            {
                OrderCode = order.OrderCode,
                CustomerId = order.CustomerId,
                SenderName = order.SenderName,
                SenderPhone = order.SenderPhone,
                SenderAddress = order.SenderAddress,
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                ReceiverAddress = order.ReceiverAddress,
                DeliveryMethod = order.DeliveryMethod,
                DistanceKm = order.DistanceKm,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TotalAmount = order.TotalAmount,
                CurrentStatus = order.CurrentStatus,
                DeliveryStaffId = order.DeliveryStaffId,
                DeliveryNote = order.DeliveryNote
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOrder(AdminEditOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadOrderDropdownsAsync();
                return View(model);
            }

            var order = await _context.DeliveryOrders.FirstOrDefaultAsync(o => o.OrderCode == model.OrderCode);
            if (order == null)
            {
                return NotFound();
            }

            bool statusChanged = order.CurrentStatus != model.CurrentStatus;

            order.CustomerId = model.CustomerId;
            order.SenderName = model.SenderName;
            order.SenderPhone = model.SenderPhone;
            order.SenderAddress = model.SenderAddress;
            order.ReceiverName = model.ReceiverName;
            order.ReceiverPhone = model.ReceiverPhone;
            order.ReceiverAddress = model.ReceiverAddress;
            order.DeliveryMethod = model.DeliveryMethod;
            order.DistanceKm = model.DistanceKm;
            order.PaymentMethod = model.PaymentMethod;
            order.PaymentStatus = model.PaymentStatus;
            order.TotalAmount = model.TotalAmount;
            order.CurrentStatus = model.CurrentStatus;
            order.DeliveryStaffId = model.DeliveryStaffId;
            order.DeliveryNote = model.DeliveryNote;

            if (model.PaymentStatus == "Đã thanh toán" && order.PaymentTime == null)
            {
                order.PaymentTime = DateTime.Now;
                order.PaymentAmount = model.TotalAmount;
            }

            if (statusChanged)
            {
                var history = new OrderStatusHistory
                {
                    OrderCode = order.OrderCode,
                    Status = model.CurrentStatus,
                    ChangedBy = User.Identity?.Name ?? "Admin",
                    ChangedAt = DateTime.Now
                };
                _context.OrderStatusHistories.Add(history);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật thông tin đơn hàng #{order.OrderCode} thành công.";
            return RedirectToAction(nameof(Orders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var order = await _context.DeliveryOrders
                .Include(o => o.OrderItems)
                .Include(o => o.OrderStatusHistories)
                .FirstOrDefaultAsync(o => o.OrderCode == id);

            if (order == null)
            {
                return NotFound();
            }

            // Xóa các bản ghi liên quan (CheckIns, Reviews nếu có)
            var checkIns = await _context.CheckIns.Where(c => c.OrderCode == id).ToListAsync();
            if (checkIns.Any())
            {
                _context.CheckIns.RemoveRange(checkIns);
            }

            var reviews = await _context.Reviews.Where(r => r.OrderCode == id).ToListAsync();
            if (reviews.Any())
            {
                _context.Reviews.RemoveRange(reviews);
            }

            _context.DeliveryOrders.Remove(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa vĩnh viễn đơn hàng #{id} khỏi hệ thống.";
            return RedirectToAction(nameof(Orders));
        }
    }
}

