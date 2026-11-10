namespace DeliveryManagement.Models
{
    /// <summary>
    /// Hằng số phương thức / trạng thái thanh toán.
    /// Giá trị Cod và Online phải khớp với value của radio trong Views/DeliveryOrder/Create.cshtml.
    /// </summary>
    public static class PaymentConstants
    {
        // Phương thức thanh toán
        public const string Cod = "Thanh toán khi giao hàng";
        public const string Online = "Thanh toán trực tuyến";

        // Trạng thái thanh toán
        public const string Unpaid = "Chưa thanh toán";
        public const string Paid = "Đã thanh toán";
        public const string Refunded = "Hoàn tiền";

        public static readonly string[] Methods = { Cod, Online };
        public static readonly string[] Statuses = { Unpaid, Paid, Refunded };
    }
}
