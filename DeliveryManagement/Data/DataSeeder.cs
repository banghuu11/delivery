using DeliveryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Data
{
    public static class DataSeeder
    {
        public static async Task SeedInitialDataAsync(ApplicationDbContext context)
        {
            // 1. SEED WEBSITE SETTINGS
            if (!await context.WebsiteSettings.AnyAsync())
            {
                var defaultSetting = new WebsiteSetting
                {
                    BrandName = "TỐC ĐỘ DELIVERY",
                    Slogan = "GIAO HÀNG HỎA TỐC, NHẬN TRONG NGÀY",
                    SubSlogan = "An Toàn - Nhanh Chóng - Tin Cậy",
                    Hotline = "1900 8888",
                    SupportEmail = "hotro@tocdodelivery.vn",
                    HeadquarterAddress = "TP. Hồ Chí Minh, Việt Nam",
                    WorkingHours = "24/7 (Cả ngày lễ và Chủ nhật)",
                    HeroBannerImageUrl = "/images/hero_courier.jpg"
                };

                await context.WebsiteSettings.AddAsync(defaultSetting);
                await context.SaveChangesAsync();
            }

            // 2. SEED HOME FEATURES (DOCK & SERVICES)
            if (!await context.HomeFeatures.AnyAsync())
            {
                var features = new List<HomeFeature>
                {
                    // 3 mục Dock nổi trên banner
                    new HomeFeature
                    {
                        Title = "Giao Hàng Hỏa Tốc",
                        Description = "Nội thành từ 1 - 3 giờ, ưu tiên lấy ngay",
                        IconClass = "bi-bicycle",
                        SectionType = "Dock",
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new HomeFeature
                    {
                        Title = "Vận Chuyển Toàn Quốc",
                        Description = "Liên tỉnh an toàn, cước phí tối ưu",
                        IconClass = "bi-truck",
                        SectionType = "Dock",
                        DisplayOrder = 2,
                        IsActive = true
                    },
                    new HomeFeature
                    {
                        Title = "Theo Dõi Thời Gian Thực",
                        Description = "Cập nhật lộ trình và check-in chính xác",
                        IconClass = "bi-stopwatch",
                        SectionType = "Dock",
                        DisplayOrder = 3,
                        IsActive = true
                    },

                    // 3 dịch vụ chính bên dưới
                    new HomeFeature
                    {
                        Title = "Giao Hỏa Tốc 2H",
                        Description = "Cam kết giao trong vòng 1-2 giờ tại nội thành. Lộ trình giao thẳng, không lưu kho trung gian.",
                        IconClass = "bi-lightning-charge-fill",
                        SectionType = "Service",
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new HomeFeature
                    {
                        Title = "Đóng Gói & Bảo Hiểm 100%",
                        Description = "Bảo hiểm hàng hóa giá trị cao, quy trình phân loại hàng dễ vỡ nghiêm ngặt và đền bù minh bạch.",
                        IconClass = "bi-shield-check",
                        SectionType = "Service",
                        DisplayOrder = 2,
                        IsActive = true
                    },
                    new HomeFeature
                    {
                        Title = "Theo Dõi Realtime 24/7",
                        Description = "Check-in tự động tại từng trạm trung chuyển, hỗ trợ khách hàng và người nhận qua hotline & tin nhắn.",
                        IconClass = "bi-clock-history",
                        SectionType = "Service",
                        DisplayOrder = 3,
                        IsActive = true
                    }
                };

                await context.HomeFeatures.AddRangeAsync(features);
                await context.SaveChangesAsync();
            }

            // 3. SEED MAP STATIONS (TRẠM & BƯU CỤC)
            if (!await context.MapStations.AnyAsync())
            {
                var stations = new List<MapStation>
                {
                    new MapStation
                    {
                        StationName = "Bưu cục Trung tâm Quận 1",
                        Address = "Số 125 Hai Bà Trưng, Phường Bến Nghé, Quận 1, TP.HCM",
                        Latitude = 10.7768890m,
                        Longitude = 106.6954440m,
                        IsActive = true
                    },
                    new MapStation
                    {
                        StationName = "Trạm phân loại kho Tân Bình",
                        Address = "Số 45 Trường Sơn, Phường 2, Tân Bình, TP.HCM",
                        Latitude = 10.8123450m,
                        Longitude = 106.6654320m,
                        IsActive = true
                    },
                    new MapStation
                    {
                        StationName = "Bưu cục Thủ Đức Hub",
                        Address = "Số 216 Võ Văn Ngân, Bình Thọ, TP. Thủ Đức, TP.HCM",
                        Latitude = 10.8512340m,
                        Longitude = 106.7712340m,
                        IsActive = true
                    },
                    new MapStation
                    {
                        StationName = "Trạm trung chuyển Hà Nội",
                        Address = "Số 88 Phạm Hùng, Mỹ Đình, Nam Từ Liêm, Hà Nội",
                        Latitude = 21.0187650m,
                        Longitude = 105.7765430m,
                        IsActive = true
                    },
                    new MapStation
                    {
                        StationName = "Bưu cục Đà Nẵng Hub",
                        Address = "Số 15 Nguyễn Tri Phương, Hải Châu, TP. Đà Nẵng",
                        Latitude = 16.0678900m,
                        Longitude = 108.2134560m,
                        IsActive = true
                    }
                };

                await context.MapStations.AddRangeAsync(stations);
                await context.SaveChangesAsync();
            }

            // 4. SEED PACKAGE TYPES
            if (!await context.PackageTypes.AnyAsync())
            {
                var packageTypes = new List<PackageType>
                {
                    new PackageType { TypeName = "Gói nhỏ", Description = "Hàng hóa có kích thước nhỏ", IsActive = true },
                    new PackageType { TypeName = "Gói bọc", Description = "Hàng hóa được bọc bên ngoài", IsActive = true },
                    new PackageType { TypeName = "Bao", Description = "Hàng hóa đóng trong bao", IsActive = true },
                    new PackageType { TypeName = "Thùng", Description = "Hàng hóa đóng trong thùng", IsActive = true },
                    new PackageType { TypeName = "Tivi", Description = "Tivi và thiết bị màn hình", IsActive = true },
                    new PackageType { TypeName = "Laptop", Description = "Máy tính xách tay", IsActive = true },
                    new PackageType { TypeName = "Máy tính", Description = "Máy tính để bàn", IsActive = true },
                    new PackageType { TypeName = "CPU", Description = "Bộ xử lý máy tính", IsActive = true },
                    new PackageType { TypeName = "Xe", Description = "Xe hoặc phương tiện cần vận chuyển", IsActive = true }
                };

                await context.PackageTypes.AddRangeAsync(packageTypes);
                await context.SaveChangesAsync();
            }

            // 5. SEED PRICE TABLES
            if (!await context.PriceTables.AnyAsync())
            {
                var priceTables = new List<PriceTable>
                {
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 0, MaxWeight = 5, MinDistance = 0, MaxDistance = 5, WeightRange = "0 - 5 kg", DistanceRange = "0 - 5 km", Price = 20000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 0, MaxWeight = 5, MinDistance = 5, MaxDistance = 10, WeightRange = "0 - 5 kg", DistanceRange = "5 - 10 km", Price = 25000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 0, MaxWeight = 5, MinDistance = 10, MaxDistance = 20, WeightRange = "0 - 5 kg", DistanceRange = "10 - 20 km", Price = 30000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 0, MaxWeight = 5, MinDistance = 20, MaxDistance = 50, WeightRange = "0 - 5 kg", DistanceRange = "20 - 50 km", Price = 40000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 5, MaxWeight = 20, MinDistance = 0, MaxDistance = 5, WeightRange = "5 - 20 kg", DistanceRange = "0 - 5 km", Price = 40000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 5, MaxWeight = 20, MinDistance = 5, MaxDistance = 10, WeightRange = "5 - 20 kg", DistanceRange = "5 - 10 km", Price = 45000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 5, MaxWeight = 20, MinDistance = 10, MaxDistance = 20, WeightRange = "5 - 20 kg", DistanceRange = "10 - 20 km", Price = 55000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 5, MaxWeight = 20, MinDistance = 20, MaxDistance = 50, WeightRange = "5 - 20 kg", DistanceRange = "20 - 50 km", Price = 70000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 20, MaxWeight = 100, MinDistance = 0, MaxDistance = 5, WeightRange = "20 - 100 kg", DistanceRange = "0 - 5 km", Price = 80000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 20, MaxWeight = 100, MinDistance = 5, MaxDistance = 10, WeightRange = "20 - 100 kg", DistanceRange = "5 - 10 km", Price = 95000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 20, MaxWeight = 100, MinDistance = 10, MaxDistance = 20, WeightRange = "20 - 100 kg", DistanceRange = "10 - 20 km", Price = 110000 },
                    new PriceTable { DeliveryMethod = "Thường", MinWeight = 20, MaxWeight = 100, MinDistance = 20, MaxDistance = 50, WeightRange = "20 - 100 kg", DistanceRange = "20 - 50 km", Price = 130000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 0, MaxWeight = 5, MinDistance = 0, MaxDistance = 5, WeightRange = "0 - 5 kg", DistanceRange = "0 - 5 km", Price = 35000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 0, MaxWeight = 5, MinDistance = 5, MaxDistance = 10, WeightRange = "0 - 5 kg", DistanceRange = "5 - 10 km", Price = 40000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 0, MaxWeight = 5, MinDistance = 10, MaxDistance = 20, WeightRange = "0 - 5 kg", DistanceRange = "10 - 20 km", Price = 50000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 0, MaxWeight = 5, MinDistance = 20, MaxDistance = 50, WeightRange = "0 - 5 kg", DistanceRange = "20 - 50 km", Price = 65000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 5, MaxWeight = 20, MinDistance = 0, MaxDistance = 5, WeightRange = "5 - 20 kg", DistanceRange = "0 - 5 km", Price = 70000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 5, MaxWeight = 20, MinDistance = 5, MaxDistance = 10, WeightRange = "5 - 20 kg", DistanceRange = "5 - 10 km", Price = 80000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 5, MaxWeight = 20, MinDistance = 10, MaxDistance = 20, WeightRange = "5 - 20 kg", DistanceRange = "10 - 20 km", Price = 95000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 5, MaxWeight = 20, MinDistance = 20, MaxDistance = 50, WeightRange = "5 - 20 kg", DistanceRange = "20 - 50 km", Price = 115000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 20, MaxWeight = 100, MinDistance = 0, MaxDistance = 5, WeightRange = "20 - 100 kg", DistanceRange = "0 - 5 km", Price = 120000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 20, MaxWeight = 100, MinDistance = 5, MaxDistance = 10, WeightRange = "20 - 100 kg", DistanceRange = "5 - 10 km", Price = 140000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 20, MaxWeight = 100, MinDistance = 10, MaxDistance = 20, WeightRange = "20 - 100 kg", DistanceRange = "10 - 20 km", Price = 160000 },
                    new PriceTable { DeliveryMethod = "Nhanh", MinWeight = 20, MaxWeight = 100, MinDistance = 20, MaxDistance = 50, WeightRange = "20 - 100 kg", DistanceRange = "20 - 50 km", Price = 190000 }
                };

                await context.PriceTables.AddRangeAsync(priceTables);
                await context.SaveChangesAsync();
            }
        }
    }
}
