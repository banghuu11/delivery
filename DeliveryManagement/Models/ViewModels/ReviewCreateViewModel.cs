
using System.ComponentModel.DataAnnotations;

namespace DeliveryManagement.Models.ViewModels
{
    public class ReviewCreateViewModel
    {
        [Required]
        public string OrderCode { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "Vui lòng chọn từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nhận xét.")]
        [StringLength(1000, ErrorMessage = "Nhận xét không được vượt quá 1000 ký tự.")]
        public string Comment { get; set; } = string.Empty;
    }
}
