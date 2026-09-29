using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class MapStation
    {
        [Key]
        public int StationId { get; set; }

        // Tên trạm
        public string StationName { get; set; } = string.Empty;

        // Địa chỉ
        public string Address { get; set; } = string.Empty;

        // Tọa độ
        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        // Trạm đang hoạt động hay không
        public bool IsActive { get; set; } = true;

        // Navigation property
        public ICollection<CheckIn> CheckIns { get; set; }
            = new List<CheckIn>();
    }
}