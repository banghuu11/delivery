using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    // Mọi tài khoản đã đăng nhập đều có thể quản lý hồ sơ cá nhân và đổi mật khẩu.
    [Authorize]
    public class SenderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public SenderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ==========================================
        // 1. HỒ SƠ CÁ NHÂN
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var isCustomer = await _userManager.IsInRoleAsync(user, "Customer");
            var savedAddresses = new List<SenderAddress>();
            var userOrders = new List<DeliveryOrder>();

            // Chỉ Customer có sổ địa chỉ gửi hàng.
            if (isCustomer)
            {
                savedAddresses = await _context.SenderAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedAt)
                    .ToListAsync();

                // Nếu khách có địa chỉ mặc định nhưng chưa có trong sổ địa chỉ,
                // tạo một bản ghi địa chỉ mặc định ban đầu.
                if (!savedAddresses.Any() &&
                    !string.IsNullOrWhiteSpace(user.DefaultSenderAddress))
                {
                    var initialAddress = new SenderAddress
                    {
                        UserId = user.Id,
                        SenderName = user.FullName ?? user.UserName ?? "Người gửi",
                        SenderPhone = user.PhoneNumber ?? string.Empty,
                        SenderAddressText = user.DefaultSenderAddress,
                        AddressTag = "Kho hàng chính",
                        IsDefault = true,
                        CreatedAt = DateTime.Now
                    };

                    _context.SenderAddresses.Add(initialAddress);
                    await _context.SaveChangesAsync();
                    savedAddresses.Add(initialAddress);
                }

                // Chỉ thống kê đơn hàng của tài khoản Customer.
                userOrders = await _context.DeliveryOrders
                    .Where(o => o.CustomerId == user.Id)
                    .ToListAsync();
            }

            var vm = new SenderProfileViewModel
            {
                UserId = user.Id,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                DefaultSenderAddress = isCustomer
                    ? user.DefaultSenderAddress
                    : null,
                SavedAddresses = savedAddresses,
                TotalOrdersSent = userOrders.Count,
                PendingOrders = userOrders.Count(o =>
                    o.CurrentStatus == "Chưa nhận" ||
                    o.CurrentStatus == "Chờ tiếp nhận" ||
                    o.CurrentStatus == "Đã nhận" ||
                    o.CurrentStatus == "Đã tiếp nhận"),
                DeliveringOrders = userOrders.Count(o =>
                    o.CurrentStatus == "Trong kho" ||
                    o.CurrentStatus == "Đang xử lý" ||
                    o.CurrentStatus == "Đang giao"),
                CompletedOrders = userOrders.Count(o =>
                    o.CurrentStatus == "Đã giao" ||
                    o.CurrentStatus == "Giao thành công"),
                TotalSpent = userOrders
                    .Where(o => o.CurrentStatus != "Đã hủy" &&
                                o.CurrentStatus != "Giao thất bại")
                    .Sum(o => o.TotalAmount)
            };

            return View(vm);
        }

        // ==========================================
        // 2. CẬP NHẬT HỒ SƠ CÁ NHÂN
        // Mọi role được sửa hồ sơ của chính mình.
        // Chỉ Customer được quản lý địa chỉ gửi hàng.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            SenderProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                TempData["ErrorMessage"] =
                    "Họ và tên không được để trống.";

                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                TempData["ErrorMessage"] =
                    "Số điện thoại liên hệ không được để trống.";

                return RedirectToAction(nameof(Index));
            }

            // Cập nhật hồ sơ của tài khoản đang đăng nhập.
            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber.Trim();

            var isCustomer = await _userManager.IsInRoleAsync(
                user,
                "Customer");

            if (isCustomer)
            {
                string? newAddress =
                    string.IsNullOrWhiteSpace(model.DefaultSenderAddress)
                        ? null
                        : model.DefaultSenderAddress.Trim();

                var savedAddresses = await _context.SenderAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedAt)
                    .ToListAsync();

                // TRƯỜNG HỢP 1:
                // Khách để trống địa chỉ mặc định.
                if (newAddress == null)
                {
                    user.DefaultSenderAddress = null;

                    // Bỏ trạng thái mặc định nhưng vẫn giữ các địa chỉ đã lưu.
                    foreach (var address in savedAddresses)
                    {
                        address.IsDefault = false;
                    }
                }
                else
                {
                    user.DefaultSenderAddress = newAddress;

                    // Tìm địa chỉ đang được đánh dấu mặc định.
                    var defaultAddress = savedAddresses
                        .FirstOrDefault(a => a.IsDefault);

                    // Nếu chưa có địa chỉ mặc định, thử tìm địa chỉ
                    // đã lưu có nội dung trùng với địa chỉ vừa nhập.
                    if (defaultAddress == null)
                    {
                        defaultAddress = savedAddresses.FirstOrDefault(a =>
                            string.Equals(
                                a.SenderAddressText?.Trim(),
                                newAddress,
                                StringComparison.OrdinalIgnoreCase));
                    }

                    // Nếu vẫn không tìm thấy, tạo một địa chỉ mới.
                    if (defaultAddress == null)
                    {
                        defaultAddress = new SenderAddress
                        {
                            UserId = user.Id,
                            SenderName = user.FullName,
                            SenderPhone = user.PhoneNumber,
                            SenderAddressText = newAddress,
                            AddressTag = "Kho hàng chính",
                            IsDefault = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.SenderAddresses.Add(defaultAddress);
                        savedAddresses.Add(defaultAddress);
                    }
                    else
                    {
                        // Đồng bộ địa chỉ mặc định với hồ sơ cá nhân.
                        defaultAddress.SenderName = user.FullName;
                        defaultAddress.SenderPhone = user.PhoneNumber;
                        defaultAddress.SenderAddressText = newAddress;
                    }

                    // Đảm bảo chỉ có một địa chỉ được đánh dấu mặc định.
                    foreach (var address in savedAddresses)
                    {
                        address.IsDefault =
                            address.Id == defaultAddress.Id;
                    }

                    defaultAddress.IsDefault = true;
                }
            }

            // Nhân viên và Admin chỉ cập nhật họ tên, số điện thoại.
            // Không cho họ thay đổi địa chỉ gửi hàng qua form này.
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(Index));
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Đã cập nhật hồ sơ cá nhân thành công!";

            return RedirectToAction(nameof(Index));
        }
        // ==========================================
        // 3. THÊM ĐỊA CHỈ GỬI HÀNG - CHỈ CUSTOMER
        // ==========================================
        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAddress(
            SenderAddressInputModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Vui lòng nhập đầy đủ thông tin bắt buộc của địa chỉ người gửi.";
                return RedirectToAction(nameof(Index));
            }

            bool hasAnyAddress = await _context.SenderAddresses
                .AnyAsync(a => a.UserId == user.Id);

            if (!hasAnyAddress)
            {
                model.IsDefault = true;
            }

            if (model.IsDefault)
            {
                var oldDefaults = await _context.SenderAddresses
                    .Where(a => a.UserId == user.Id && a.IsDefault)
                    .ToListAsync();

                foreach (var old in oldDefaults)
                {
                    old.IsDefault = false;
                }

                user.DefaultSenderAddress =
                    model.SenderAddressText.Trim();

                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(
                        "; ",
                        updateResult.Errors.Select(e => e.Description));

                    return RedirectToAction(nameof(Index));
                }
            }

            var newAddress = new SenderAddress
            {
                UserId = user.Id,
                SenderName = model.SenderName.Trim(),
                SenderPhone = model.SenderPhone.Trim(),
                SenderAddressText = model.SenderAddressText.Trim(),
                AddressTag = string.IsNullOrWhiteSpace(model.AddressTag)
                    ? "Địa chỉ gửi hàng"
                    : model.AddressTag.Trim(),
                IsDefault = model.IsDefault,
                CreatedAt = DateTime.Now
            };

            _context.SenderAddresses.Add(newAddress);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã thêm địa chỉ '{newAddress.AddressTag}' vào sổ địa chỉ người gửi!";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 4. SỬA ĐỊA CHỈ GỬI HÀNG - CHỈ CUSTOMER
        // ==========================================
        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAddress(
            SenderAddressInputModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Dữ liệu cập nhật địa chỉ không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var address = await _context.SenderAddresses
                .FirstOrDefaultAsync(a =>
                    a.Id == model.Id &&
                    a.UserId == user.Id);

            if (address == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy địa chỉ người gửi cần sửa.";

                return RedirectToAction(nameof(Index));
            }

            address.SenderName = model.SenderName.Trim();
            address.SenderPhone = model.SenderPhone.Trim();
            address.SenderAddressText = model.SenderAddressText.Trim();

            address.AddressTag = string.IsNullOrWhiteSpace(model.AddressTag)
                ? "Địa chỉ gửi hàng"
                : model.AddressTag.Trim();

            if (model.IsDefault && !address.IsDefault)
            {
                var oldDefaults = await _context.SenderAddresses
                    .Where(a => a.UserId == user.Id && a.IsDefault)
                    .ToListAsync();

                foreach (var old in oldDefaults)
                {
                    old.IsDefault = false;
                }

                address.IsDefault = true;
                user.DefaultSenderAddress = address.SenderAddressText;

                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(
                        "; ",
                        updateResult.Errors.Select(e => e.Description));

                    return RedirectToAction(nameof(Index));
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Đã cập nhật thông tin địa chỉ người gửi thành công!";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 5. ĐẶT ĐỊA CHỈ MẶC ĐỊNH - CHỈ CUSTOMER
        // ==========================================
        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var targetAddress = await _context.SenderAddresses
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.UserId == user.Id);

            if (targetAddress == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy địa chỉ.";
                return RedirectToAction(nameof(Index));
            }

            var allAddresses = await _context.SenderAddresses
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            foreach (var address in allAddresses)
            {
                address.IsDefault = address.Id == id;
            }

            user.DefaultSenderAddress = targetAddress.SenderAddressText;
            user.FullName = targetAddress.SenderName;
            user.PhoneNumber = targetAddress.SenderPhone;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(
                    "; ",
                    updateResult.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(Index));
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã đặt '{targetAddress.AddressTag}' làm địa chỉ người gửi mặc định.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 6. XÓA ĐỊA CHỈ GỬI HÀNG - CHỈ CUSTOMER
        // ==========================================
        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var address = await _context.SenderAddresses
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.UserId == user.Id);

            if (address == null)
            {
                TempData["ErrorMessage"] =
                    "Không tìm thấy địa chỉ cần xóa.";

                return RedirectToAction(nameof(Index));
            }

            bool wasDefault = address.IsDefault;

            _context.SenderAddresses.Remove(address);
            await _context.SaveChangesAsync();

            // Nếu xóa địa chỉ mặc định thì gán cho địa chỉ còn lại.
            if (wasDefault)
            {
                var firstRemaining = await _context.SenderAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                if (firstRemaining != null)
                {
                    firstRemaining.IsDefault = true;
                    user.DefaultSenderAddress =
                        firstRemaining.SenderAddressText;

                    var updateResult = await _userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = string.Join(
                            "; ",
                            updateResult.Errors.Select(e => e.Description));

                        return RedirectToAction(nameof(Index));
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    user.DefaultSenderAddress = null;

                    var updateResult = await _userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        TempData["ErrorMessage"] = string.Join(
                            "; ",
                            updateResult.Errors.Select(e => e.Description));

                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            TempData["SuccessMessage"] =
                "Đã xóa địa chỉ khỏi sổ địa chỉ người gửi thành công.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 7. ĐỔI MẬT KHẨU - MỌI ROLE ĐÃ ĐĂNG NHẬP
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordInputModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Thông tin đổi mật khẩu không hợp lệ. Vui lòng kiểm tra lại.";

                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 8. API SỔ ĐỊA CHỈ - CHỈ CUSTOMER
        // ==========================================
        [Authorize(Roles = "Customer")]
        [HttpGet]
        public async Task<IActionResult> GetAddressesJson()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var list = await _context.SenderAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.SenderName,
                    a.SenderPhone,
                    a.SenderAddressText,
                    a.AddressTag,
                    a.IsDefault
                })
                .ToListAsync();

            return Json(list);
        }
    }
}