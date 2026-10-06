using DeliveryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Data
{
    public static class DataSeeder
    {
        public static async Task SeedInitialDataAsync(ApplicationDbContext context)
        {
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
