using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class CheckIn
    {
        [Key]
        public int CheckInId { get; set; }

        // Đơn hàng
        public string OrderCode { get; set; } = string.Empty;

        // Trạm check-in
        public int StationId { get; set; }

        // Nhân viên giao hàng
        public string DeliveryStaffId { get; set; } = string.Empty;

        // Vị trí thực tế lúc check-in
        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        // Thời gian check-in
        public DateTime CheckInTime { get; set; } = DateTime.Now;

        // Navigation properties
        public DeliveryOrder? DeliveryOrder { get; set; }

        public MapStation? MapStation { get; set; }

        public ApplicationUser? DeliveryStaff { get; set; }
    }
}