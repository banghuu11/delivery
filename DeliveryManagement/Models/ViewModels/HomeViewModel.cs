using DeliveryManagement.Models;

namespace DeliveryManagement.Models.ViewModels
{
    public class HomeViewModel
    {
        public WebsiteSetting Setting { get; set; } = new();
        public List<HomeFeature> DockFeatures { get; set; } = new();
        public List<HomeFeature> MainServices { get; set; } = new();
        public List<PackageType> PackageTypes { get; set; } = new();
        public List<PriceTable> PriceHighlights { get; set; } = new();
        public List<MapStation> Stations { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();

        // Thống kê động lấy trực tiếp từ Database
        public int TotalOrders { get; set; }
        public int TotalDeliveredOrders { get; set; }
        public int TotalStations { get; set; }
        public int TotalCustomers { get; set; }
    }
}
