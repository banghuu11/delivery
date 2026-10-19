using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models
{
    public class HomeFeature
    {
        [Key]
        public int FeatureId { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string IconClass { get; set; } = "bi-box-seam";

        [MaxLength(50)]
        public string BadgeText { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 0;

        // "Dock" = 3 mục nổi trên dock, "Service" = Dịch vụ nổi bật, "Highlight" = Điểm mạnh
        [MaxLength(50)]
        public string SectionType { get; set; } = "Dock";

        public bool IsActive { get; set; } = true;
    }
}
