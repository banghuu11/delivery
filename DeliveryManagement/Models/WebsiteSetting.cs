using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class WebsiteSetting
    {
        [Key]
        public int SettingId { get; set; }

        [MaxLength(100)]
        public string BrandName { get; set; } = "TỐC ĐỘ DELIVERY";

        [MaxLength(200)]
        public string Slogan { get; set; } = "Giao Hàng Hỏa Tốc, Nhận Trong Ngày";

        [MaxLength(200)]
        public string SubSlogan { get; set; } = "An Toàn - Nhanh Chóng - Tin Cậy";

        [MaxLength(50)]
        public string Hotline { get; set; } = "1900 8888";

        [MaxLength(100)]
        public string SupportEmail { get; set; } = "hotro@tocdodelivery.vn";

        [MaxLength(300)]
        public string HeadquarterAddress { get; set; } = "TP. Hồ Chí Minh, Việt Nam";

        [MaxLength(100)]
        public string WorkingHours { get; set; } = "24/7 (Cả ngày lễ và Chủ nhật)";

        [MaxLength(500)]
        public string HeroBannerImageUrl { get; set; } = "/images/hero_courier.jpg";
    }
}
