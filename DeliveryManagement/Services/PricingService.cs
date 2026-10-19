using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace DeliveryManagement.Services
{
    public class PricingService : IPricingService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PricingService> _logger;

        // Tọa độ mặc định trung tâm TP.HCM nếu client không cấp quyền GPS
        private const double DefaultLat = 10.8231;
        private const double DefaultLon = 106.6297;

        // Thời gian lưu cache cho cả Thời tiết và Giao thông (1 tiếng = 60 phút)
        private static readonly TimeSpan ApiCacheDuration = TimeSpan.FromHours(1);

        private record WeatherCacheData(
            double Temperature,
            double RainMm,
            decimal Surcharge,
            string Description,
            string Icon,
            bool IsSurchargeApplied);

        private record TrafficCacheData(
            decimal Surcharge,
            string Description,
            string Icon,
            bool IsSurchargeApplied);

        public PricingService(
            ApplicationDbContext context,
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<PricingService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _cache = cache;
            _httpClient.Timeout = TimeSpan.FromSeconds(3); // Giới hạn 3s để không nghẽn request
            _logger = logger;
        }

        public async Task<PricingPolicy> GetActivePolicyAsync()
        {
            var policy = await _context.PricingPolicies.FirstOrDefaultAsync();
            if (policy == null)
            {
                policy = new PricingPolicy
                {
                    BaseDistanceKm = 2.0m,
                    StandardBasePrice = 15000m,
                    StandardPricePerKm = 5000m,
                    ExpressBasePrice = 25000m,
                    ExpressPricePerKm = 8000m,
                    WeightThresholdKg = 3.0m,
                    ExtraPricePerKg = 3000m,
                    WeatherRainSurcharge = 15000m,
                    WeatherStormSurcharge = 25000m,
                    TrafficJamSurcharge = 10000m,
                    EnableWeatherSurcharge = true,
                    EnableTrafficSurcharge = true,
                    UpdatedAt = DateTime.Now
                };
                _context.PricingPolicies.Add(policy);
                await _context.SaveChangesAsync();
            }
            return policy;
        }

        public async Task UpdatePolicyAsync(PricingPolicy updatedPolicy)
        {
            var policy = await GetActivePolicyAsync();

            policy.BaseDistanceKm = updatedPolicy.BaseDistanceKm;
            policy.StandardBasePrice = updatedPolicy.StandardBasePrice;
            policy.StandardPricePerKm = updatedPolicy.StandardPricePerKm;
            policy.ExpressBasePrice = updatedPolicy.ExpressBasePrice;
            policy.ExpressPricePerKm = updatedPolicy.ExpressPricePerKm;
            policy.WeightThresholdKg = updatedPolicy.WeightThresholdKg;
            policy.ExtraPricePerKg = updatedPolicy.ExtraPricePerKg;
            policy.WeatherRainSurcharge = updatedPolicy.WeatherRainSurcharge;
            policy.WeatherStormSurcharge = updatedPolicy.WeatherStormSurcharge;
            policy.TrafficJamSurcharge = updatedPolicy.TrafficJamSurcharge;
            policy.EnableWeatherSurcharge = updatedPolicy.EnableWeatherSurcharge;
            policy.EnableTrafficSurcharge = updatedPolicy.EnableTrafficSurcharge;
            policy.TomTomApiKey = updatedPolicy.TomTomApiKey?.Trim();
            policy.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task<PriceCalculationResult> CalculatePriceAsync(
            string deliveryMethod,
            decimal weight,
            decimal distanceKm,
            double? lat = null,
            double? lon = null)
        {
            var result = new PriceCalculationResult
            {
                DeliveryMethod = string.IsNullOrWhiteSpace(deliveryMethod) ? "Thường" : deliveryMethod.Trim(),
                DistanceKm = Math.Max(0.1m, distanceKm),
                WeightKg = Math.Max(0.1m, weight)
            };

            var policy = await GetActivePolicyAsync();
            double useLat = (lat.HasValue && lat.Value != 0) ? lat.Value : DefaultLat;
            double useLon = (lon.HasValue && lon.Value != 0) ? lon.Value : DefaultLon;

            // 1. TÍNH CƯỚC THEO CỰ LY (KM)
            bool isExpress = result.DeliveryMethod.Equals("Nhanh", StringComparison.OrdinalIgnoreCase);
            decimal baseDistance = Math.Max(0.1m, policy.BaseDistanceKm);
            decimal baseFee = isExpress ? policy.ExpressBasePrice : policy.StandardBasePrice;
            decimal pricePerKm = isExpress ? policy.ExpressPricePerKm : policy.StandardPricePerKm;

            if (result.DistanceKm <= baseDistance)
            {
                result.BaseShippingFee = baseFee;
            }
            else
            {
                decimal extraDistance = result.DistanceKm - baseDistance;
                result.BaseShippingFee = baseFee + (extraDistance * pricePerKm);
            }

            // 2. TÍNH PHỤ PHÍ KHỐI LƯỢNG (KG)
            if (result.WeightKg > policy.WeightThresholdKg)
            {
                decimal extraWeight = result.WeightKg - policy.WeightThresholdKg;
                result.WeightFee = Math.Ceiling(extraWeight) * policy.ExtraPricePerKg;
            }
            else
            {
                result.WeightFee = 0;
            }

            // 3. TÍNH PHỤ PHÍ THỜI TIẾT (OPEN-METEO API)
            if (policy.EnableWeatherSurcharge)
            {
                await EvaluateWeatherAsync(useLat, useLon, policy, result);
            }
            else
            {
                result.WeatherDescription = "Đã tắt phụ phí thời tiết";
                result.WeatherIcon = "bi bi-sun-fill text-warning";
                result.WeatherFee = 0;
            }

            // 4. TÍNH PHỤ PHÍ GIAO THÔNG & GIỜ CAO ĐIỂM
            if (policy.EnableTrafficSurcharge)
            {
                await EvaluateTrafficAsync(useLat, useLon, policy, result);
            }
            else
            {
                result.TrafficDescription = "Đã tắt phụ phí giao thông";
                result.TrafficIcon = "bi bi-car-front-fill text-primary";
                result.TrafficFee = 0;
            }

            // 5. TỔNG CƯỚC CUỐI CÙNG
            result.TotalPrice = result.BaseShippingFee + result.WeightFee + result.WeatherFee + result.TrafficFee;
            result.Success = true;
            result.Message = "Tính cước thành công";

            return result;
        }

        // ==========================================
        // CALL OPEN-METEO WEATHER API
        // ==========================================
        private async Task EvaluateWeatherAsync(
            double lat, 
            double lon, 
            PricingPolicy policy, 
            PriceCalculationResult result)
        {
            // Cache theo tọa độ làm tròn ~1.1km trong 1 tiếng (60 phút) để tối ưu tốc độ và không spam API
            string cacheKey = $"weather_{lat:F2}_{lon:F2}";
            if (_cache.TryGetValue(cacheKey, out WeatherCacheData? cached) && cached != null)
            {
                result.CurrentTemperature = cached.Temperature;
                result.CurrentRainMm = cached.RainMm;
                result.WeatherFee = cached.Surcharge;
                result.WeatherDescription = cached.Description;
                result.WeatherIcon = cached.Icon;
                result.IsWeatherSurchargeApplied = cached.IsSurchargeApplied;
                return;
            }

            try
            {
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,precipitation,rain,weather_code";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);

                    if (doc.RootElement.TryGetProperty("current", out var current))
                    {
                        double temp = current.GetProperty("temperature_2m").GetDouble();
                        double rain = current.GetProperty("rain").GetDouble();
                        int code = current.GetProperty("weather_code").GetInt32();

                        result.CurrentTemperature = temp;
                        result.CurrentRainMm = rain;

                        // Phân tích mã thời tiết WMO
                        if (code >= 95) // Dông bão, sấm sét
                        {
                            result.WeatherFee = policy.WeatherStormSurcharge;
                            result.WeatherDescription = $"Dông bão ({temp}°C) - Phụ thu thời tiết xấu";
                            result.WeatherIcon = "bi bi-cloud-lightning-rain-fill text-danger";
                            result.IsWeatherSurchargeApplied = true;
                        }
                        else if (code >= 61 || rain >= 0.5) // Mưa rào hoặc lượng mưa đáng kể
                        {
                            result.WeatherFee = policy.WeatherRainSurcharge;
                            result.WeatherDescription = $"Mưa rào/to ({temp}°C, mưa {rain}mm)";
                            result.WeatherIcon = "bi bi-cloud-rain-heavy-fill text-primary";
                            result.IsWeatherSurchargeApplied = true;
                        }
                        else if (code >= 51 || rain > 0) // Mưa phùn nhẹ
                        {
                            result.WeatherFee = Math.Round(policy.WeatherRainSurcharge * 0.5m, 0);
                            result.WeatherDescription = $"Mưa phùn nhẹ ({temp}°C)";
                            result.WeatherIcon = "bi bi-cloud-drizzle-fill text-info";
                            result.IsWeatherSurchargeApplied = true;
                        }
                        else
                        {
                            result.WeatherFee = 0;
                            result.WeatherDescription = $"Trời quang/thuận lợi ({temp}°C)";
                            result.WeatherIcon = "bi bi-sun-fill text-warning";
                            result.IsWeatherSurchargeApplied = false;
                        }

                        // Lưu cache 1 tiếng (60 phút)
                        _cache.Set(cacheKey, new WeatherCacheData(
                            result.CurrentTemperature ?? temp,
                            result.CurrentRainMm ?? rain,
                            result.WeatherFee,
                            result.WeatherDescription,
                            result.WeatherIcon,
                            result.IsWeatherSurchargeApplied), ApiCacheDuration);

                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể kết nối Open-Meteo API, sử dụng mặc định.");
            }

            // Fallback nếu không gọi được API
            result.WeatherFee = 0;
            result.WeatherDescription = "Bình thường (Ngoại tuyến)";
            result.WeatherIcon = "bi bi-cloud-sun-fill text-warning";
        }

        // ==========================================
        // CALL TOMTOM TRAFFIC HOẶC GIỜ CAO ĐIỂM
        // ==========================================
        private async Task EvaluateTrafficAsync(
            double lat, 
            double lon, 
            PricingPolicy policy, 
            PriceCalculationResult result)
        {
            // Cache mật độ giao thông trong 1 tiếng (60 phút) theo tọa độ
            string cacheKey = $"traffic_{lat:F2}_{lon:F2}";
            if (_cache.TryGetValue(cacheKey, out TrafficCacheData? cached) && cached != null)
            {
                result.TrafficFee = cached.Surcharge;
                result.TrafficDescription = cached.Description;
                result.TrafficIcon = cached.Icon;
                result.IsTrafficSurchargeApplied = cached.IsSurchargeApplied;
                return;
            }

            // 1. Thử gọi TomTom nếu Admin có cấu hình API Key
            if (!string.IsNullOrWhiteSpace(policy.TomTomApiKey))
            {
                try
                {
                    var url = $"https://api.tomtom.com/traffic/services/4/flowSegmentData/relative0/10/json?point={lat:F5}%2C{lon:F5}&key={policy.TomTomApiKey}";
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        using var stream = await response.Content.ReadAsStreamAsync();
                        using var doc = await JsonDocument.ParseAsync(stream);

                        if (doc.RootElement.TryGetProperty("flowSegmentData", out var flow))
                        {
                            double currentSpeed = flow.GetProperty("currentSpeed").GetDouble();
                            double freeFlow = flow.GetProperty("freeFlowSpeed").GetDouble();

                            if (freeFlow > 0 && currentSpeed < (freeFlow * 0.65))
                            {
                                result.TrafficFee = policy.TrafficJamSurcharge;
                                result.TrafficDescription = $"Kẹt xe tại khu vực ({currentSpeed:F0}/{freeFlow:F0} km/h - TomTom)";
                                result.TrafficIcon = "bi bi-stoplights-fill text-danger";
                                result.IsTrafficSurchargeApplied = true;

                                _cache.Set(cacheKey, new TrafficCacheData(
                                    result.TrafficFee,
                                    result.TrafficDescription,
                                    result.TrafficIcon,
                                    result.IsTrafficSurchargeApplied), ApiCacheDuration);

                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TomTom API gặp lỗi, chuyển sang cơ chế giờ cao điểm tự động.");
                }
            }

            // 2. Cơ chế Giờ cao điểm thông minh (Smart Peak-Hours Fallback)
            // Giờ Việt Nam (UTC+7)
            var nowVietnam = DateTime.UtcNow.AddHours(7);
            var timeOfDay = nowVietnam.TimeOfDay;

            // Khung sáng: 07:00 - 09:00 | Khung chiều: 16:30 - 18:45
            bool isMorningPeak = timeOfDay >= new TimeSpan(7, 0, 0) && timeOfDay <= new TimeSpan(9, 0, 0);
            bool isEveningPeak = timeOfDay >= new TimeSpan(16, 30, 0) && timeOfDay <= new TimeSpan(18, 45, 0);

            if (isMorningPeak || isEveningPeak)
            {
                string peakName = isMorningPeak ? "sáng (07:00 - 09:00)" : "chiều (16:30 - 18:45)";
                result.TrafficFee = policy.TrafficJamSurcharge;
                result.TrafficDescription = $"Giờ cao điểm tan tầm {peakName}";
                result.TrafficIcon = "bi bi-stoplights-fill text-danger";
                result.IsTrafficSurchargeApplied = true;
            }
            else
            {
                result.TrafficFee = 0;
                result.TrafficDescription = "Giao thông thông thoáng";
                result.TrafficIcon = "bi bi-car-front-fill text-primary";
                result.IsTrafficSurchargeApplied = false;
            }

            _cache.Set(cacheKey, new TrafficCacheData(
                result.TrafficFee,
                result.TrafficDescription,
                result.TrafficIcon,
                result.IsTrafficSurchargeApplied), ApiCacheDuration);
        }
    }
}
