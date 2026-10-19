using DeliveryManagement.Data;
using DeliveryManagement.Models;
using DeliveryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PriceTableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPricingService _pricingService;

        public PriceTableController(
            ApplicationDbContext context,
            IPricingService pricingService)
        {
            _context = context;
            _pricingService = pricingService;
        }

        // Cấu hình chính sách giá động (Theo km, Thời tiết, Giao thông)
        [HttpGet]
        public async Task<IActionResult> Policy()
        {
            var policy = await _pricingService.GetActivePolicyAsync();
            return View(policy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Policy(PricingPolicy model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _pricingService.UpdatePolicyAsync(model);
            TempData["SuccessMessage"] = "Đã cập nhật chính sách giá động (theo km, thời tiết và giao thông) thành công.";
            return RedirectToAction(nameof(Policy));
        }

        // Danh sách bảng giá bậc thang cũ
        public async Task<IActionResult> Index()
        {
            var list = await _context.PriceTables
                .OrderBy(p => p.DeliveryMethod)
                .ThenBy(p => p.MinWeight)
                .ThenBy(p => p.MinDistance)
                .ToListAsync();

            ViewBag.ActivePolicy = await _pricingService.GetActivePolicyAsync();
            return View(list);
        }

        // Thêm
        [HttpGet]
        public IActionResult Create()
        {
            return View("Form", new PriceTable { DeliveryMethod = "Thường" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PriceTable model)
        {
            if (!await ValidateAsync(model))
            {
                return View("Form", model);
            }

            model.PriceTableId = 0;
            Normalize(model);

            _context.PriceTables.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm mức giá.";
            return RedirectToAction(nameof(Index));
        }

        // Sửa
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _context.PriceTables.FindAsync(id);
            if (entity == null) return NotFound();

            return View("Form", entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PriceTable model)
        {
            if (!await ValidateAsync(model))
            {
                return View("Form", model);
            }

            var entity = await _context.PriceTables.FindAsync(model.PriceTableId);
            if (entity == null) return NotFound();

            Normalize(model);

            entity.DeliveryMethod = model.DeliveryMethod;
            entity.MinWeight = model.MinWeight;
            entity.MaxWeight = model.MaxWeight;
            entity.WeightRange = model.WeightRange;
            entity.DistanceRange = model.DistanceRange;
            entity.Price = model.Price;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật mức giá.";
            return RedirectToAction(nameof(Index));
        }

        // Xóa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.PriceTables.FindAsync(id);

            if (entity != null)
            {
                _context.PriceTables.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa mức giá.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // HỖ TRỢ
        // =========================

        private async Task<bool> ValidateAsync(PriceTable m)
        {
            // Hai trường này do server tự điền
            ModelState.Remove(nameof(PriceTable.WeightRange));
            ModelState.Remove(nameof(PriceTable.DistanceRange));

            if (m.MaxWeight <= m.MinWeight)
            {
                ModelState.AddModelError(
                    nameof(PriceTable.MaxWeight),
                    "Khối lượng tối đa phải lớn hơn khối lượng tối thiểu.");
            }
            else
            {
                var overlap = await _context.PriceTables.AnyAsync(p =>
                    p.PriceTableId != m.PriceTableId &&
                    p.DeliveryMethod == m.DeliveryMethod &&
                    p.MinWeight < m.MaxWeight && m.MinWeight < p.MaxWeight &&
                    p.MinDistance < m.MaxDistance && m.MinDistance < p.MaxDistance);

                if (overlap)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Khoảng khối lượng và khoảng cách bị trùng với một mức giá khác của cùng hình thức giao hàng.");
                }
            }

            return ModelState.IsValid;
        }

        private static void Normalize(PriceTable m)
        {
            m.WeightRange = $"{m.MinWeight:0.##} - {m.MaxWeight:0.##} kg";
            m.DistanceRange = m.DistanceRange?.Trim() ?? string.Empty;
        }
    }
}
