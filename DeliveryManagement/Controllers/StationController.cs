using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Station
        public async Task<IActionResult> Index(string search = "")
        {
            var query = _context.MapStations
                .Include(s => s.CheckIns)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x => x.StationName.Contains(s) || x.Address.Contains(s));
            }

            var stations = await query
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.StationName)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.TotalActive = await _context.MapStations.CountAsync(s => s.IsActive);
            ViewBag.TotalInactive = await _context.MapStations.CountAsync(s => !s.IsActive);

            return View(stations);
        }

        // GET: /Station/Create
        [HttpGet]
        public IActionResult Create()
        {
            var model = new MapStation
            {
                Latitude = 10.7768890m,
                Longitude = 106.6954440m,
                IsActive = true
            };
            return View(model);
        }

        // POST: /Station/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MapStation model)
        {
            if (string.IsNullOrWhiteSpace(model.StationName))
            {
                ModelState.AddModelError("StationName", "Tên bưu cục không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(model.Address))
            {
                ModelState.AddModelError("Address", "Địa chỉ không được để trống.");
            }

            if (model.Latitude == 0 || model.Longitude == 0)
            {
                ModelState.AddModelError("Latitude", "Vui lòng chọn tọa độ hợp lệ trên bản đồ.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.MapStations.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm bưu cục \"{model.StationName}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Station/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var station = await _context.MapStations.FindAsync(id);
            if (station == null)
            {
                return NotFound();
            }

            return View(station);
        }

        // POST: /Station/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MapStation model)
        {
            if (id != model.StationId)
            {
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(model.StationName))
            {
                ModelState.AddModelError("StationName", "Tên bưu cục không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(model.Address))
            {
                ModelState.AddModelError("Address", "Địa chỉ không được để trống.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existing = await _context.MapStations.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.StationName = model.StationName.Trim();
            existing.Address = model.Address.Trim();
            existing.Latitude = model.Latitude;
            existing.Longitude = model.Longitude;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật thông tin bưu cục \"{existing.StationName}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Station/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var station = await _context.MapStations.FindAsync(id);
            if (station == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bưu cục." });
            }

            station.IsActive = !station.IsActive;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isActive = station.IsActive,
                message = station.IsActive ? "Bưu cục đã được mở hoạt động." : "Bưu cục đã tạm ngừng hoạt động."
            });
        }

        // POST: /Station/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var station = await _context.MapStations
                .Include(s => s.CheckIns)
                .FirstOrDefaultAsync(s => s.StationId == id);

            if (station == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bưu cục cần xóa.";
                return RedirectToAction(nameof(Index));
            }

            if (station.CheckIns != null && station.CheckIns.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa bưu cục \"{station.StationName}\" vì đã có {station.CheckIns.Count} lượt check-in đơn hàng. Bạn có thể chọn Tạm ngừng hoạt động thay thế.";
                return RedirectToAction(nameof(Index));
            }

            _context.MapStations.Remove(station);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa bưu cục \"{station.StationName}\" thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
