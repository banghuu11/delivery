using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    /// <summary>
    /// Vị trí mới nhất của shipper cho một đơn đang giao.
    /// Mỗi đơn chỉ có một dòng, được ghi đè mỗi lần shipper gửi tọa độ mới.
    /// </summary>
    public class ShipperLocation
    {
        [Key]
        public string OrderCode { get; set; } = string.Empty;

        public string DeliveryStaffId { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // Sai số GPS (mét), có thể không có.
        public double? Accuracy { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
