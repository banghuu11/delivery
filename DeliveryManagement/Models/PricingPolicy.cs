using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class PricingPolicy
    {
        [Key]
        public int PricingPolicyId { get; set; }

        // Cự ly mở cửa (km)
        [Precision(10, 2)]
        [Range(0.1, 100, ErrorMessage = "Cự ly mở cửa không hợp lệ")]
        [Display(Name = "Cự ly mở cửa (km)")]
        public decimal BaseDistanceKm { get; set; } = 2.0m;

        // Giá mở cửa - Giao thường
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Giá không hợp lệ")]
        [Display(Name = "Giá mở cửa Giao thường (VNĐ)")]
        public decimal StandardBasePrice { get; set; } = 15000m;

        // Đơn giá mỗi km tiếp theo - Giao thường
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Giá không hợp lệ")]
        [Display(Name = "Mỗi km tiếp theo Giao thường (VNĐ)")]
        public decimal StandardPricePerKm { get; set; } = 5000m;

        // Giá mở cửa - Giao nhanh
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Giá không hợp lệ")]
        [Display(Name = "Giá mở cửa Giao nhanh (VNĐ)")]
        public decimal ExpressBasePrice { get; set; } = 25000m;

        // Đơn giá mỗi km tiếp theo - Giao nhanh
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Giá không hợp lệ")]
        [Display(Name = "Mỗi km tiếp theo Giao nhanh (VNĐ)")]
        public decimal ExpressPricePerKm { get; set; } = 8000m;

        // Ngưỡng khối lượng cơ bản (kg)
        [Precision(10, 2)]
        [Range(0.1, 1000, ErrorMessage = "Ngưỡng khối lượng không hợp lệ")]
        [Display(Name = "Khối lượng cơ bản miễn phí phụ thu (kg)")]
        public decimal WeightThresholdKg { get; set; } = 3.0m;

        // Phụ phí mỗi kg vượt ngưỡng
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Phụ phí không hợp lệ")]
        [Display(Name = "Phụ phí mỗi kg vượt ngưỡng (VNĐ)")]
        public decimal ExtraPricePerKg { get; set; } = 3000m;

        // Phụ phí thời tiết: Mưa rào/to
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Phụ phí không hợp lệ")]
        [Display(Name = "Phụ phí mưa to (VNĐ)")]
        public decimal WeatherRainSurcharge { get; set; } = 15000m;

        // Phụ phí thời tiết: Bão/giông
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Phụ phí không hợp lệ")]
        [Display(Name = "Phụ phí dông bão (VNĐ)")]
        public decimal WeatherStormSurcharge { get; set; } = 25000m;

        // Phụ phí giao thông / giờ cao điểm / kẹt xe
        [Precision(18, 2)]
        [Range(0, 10000000, ErrorMessage = "Phụ phí không hợp lệ")]
        [Display(Name = "Phụ phí kẹt xe / Giờ cao điểm (VNĐ)")]
        public decimal TrafficJamSurcharge { get; set; } = 10000m;

        // Bật/tắt các loại phụ phí
        [Display(Name = "Bật phụ phí thời tiết")]
        public bool EnableWeatherSurcharge { get; set; } = true;

        [Display(Name = "Bật phụ phí giao thông")]
        public bool EnableTrafficSurcharge { get; set; } = true;

        // API Key TomTom (tùy chọn)
        [Display(Name = "TomTom Traffic API Key (Tùy chọn)")]
        public string? TomTomApiKey { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
