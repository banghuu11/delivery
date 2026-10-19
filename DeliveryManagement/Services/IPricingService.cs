using DeliveryManagement.Models;

namespace DeliveryManagement.Services
{
    public class PriceCalculationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        // Các thành phần cấu thành giá
        public decimal BaseShippingFee { get; set; }       // Cước theo cự ly (km)
        public decimal WeightFee { get; set; }             // Phụ phí khối lượng (kg)
        public decimal WeatherFee { get; set; }            // Phụ phí thời tiết
        public decimal TrafficFee { get; set; }            // Phụ phí giao thông / giờ cao điểm
        public decimal TotalPrice { get; set; }            // Tổng cước cuối cùng

        // Thông tin mô tả & minh họa
        public string WeatherDescription { get; set; } = "Bình thường";
        public string TrafficDescription { get; set; } = "Bình thường";
        public string WeatherIcon { get; set; } = "bi bi-sun-fill text-warning";
        public string TrafficIcon { get; set; } = "bi bi-car-front-fill text-primary";

        public double? CurrentTemperature { get; set; }
        public double? CurrentRainMm { get; set; }

        public bool IsWeatherSurchargeApplied { get; set; }
        public bool IsTrafficSurchargeApplied { get; set; }

        public string DeliveryMethod { get; set; } = "Thường";
        public decimal DistanceKm { get; set; }
        public decimal WeightKg { get; set; }
    }

    public interface IPricingService
    {
        Task<PricingPolicy> GetActivePolicyAsync();
        Task UpdatePolicyAsync(PricingPolicy policy);
        Task<PriceCalculationResult> CalculatePriceAsync(
            string deliveryMethod, 
            decimal weight, 
            decimal distanceKm, 
            double? lat = null, 
            double? lon = null);
    }
}
