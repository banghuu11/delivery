namespace DeliveryManagement.Services
{
    public record PaymentResult(bool Success, string Message);

    public interface IPaymentService
    {
        /// <summary>Khách thanh toán trực tuyến cho đơn của chính mình.</summary>
        Task<PaymentResult> PayOnlineAsync(string orderCode, string userId);

        /// <summary>Khách đổi phương thức thanh toán khi đơn chưa được tiếp nhận và chưa thanh toán.</summary>
        Task<PaymentResult> ChangeMethodAsync(string orderCode, string userId, string newMethod);

        /// <summary>
        /// Ghi nhận đã thu tiền COD khi giao hàng thành công
        /// (gọi từ luồng shipper / kho khi chuyển đơn sang "Đã Giao").
        /// Không làm gì nếu đơn không phải COD hoặc đã thanh toán.
        /// </summary>
        Task<bool> CollectCodAsync(string orderCode);
    }
}
