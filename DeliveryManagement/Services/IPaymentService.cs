namespace DeliveryManagement.Services
{
    public interface IPaymentService
    {
        /// <summary>
        /// Ghi nhận đã thu tiền COD khi giao hàng thành công
        /// (gọi từ luồng shipper khi chuyển đơn sang "Đã Giao").
        /// Không làm gì nếu đơn không phải COD hoặc đã thanh toán.
        /// </summary>
        Task<bool> CollectCodAsync(string orderCode);
    }
}
