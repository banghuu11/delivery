using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class PriceTable
    {
        [Key]
        public int PriceTableId { get; set; }

        // Hình thức giao hàng
        public string DeliveryMethod { get; set; } = string.Empty;

        // Khoảng khối lượng
        public string WeightRange { get; set; } = string.Empty;

        // Khoảng cách
        public string DistanceRange { get; set; } = string.Empty;

        // Giá
        public decimal Price { get; set; }
    }
}