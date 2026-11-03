using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Services
{
    public class PaymentService : IPaymentService
    {
        private const string CancelledStatus = "Đã hủy";
        private const string NotReceivedStatus = "Chưa nhận";

        private readonly ApplicationDbContext _context;

        public PaymentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentResult> PayOnlineAsync(string orderCode, string userId)
        {
            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.CustomerId == userId);

            if (order == null)
            {
                return new PaymentResult(false, "Không tìm thấy đơn hàng hoặc bạn không có quyền thanh toán đơn này.");
            }

            if (order.CurrentStatus == CancelledStatus)
            {
                return new PaymentResult(false, "Đơn hàng đã hủy, không thể thanh toán.");
            }

            if (order.PaymentStatus == PaymentConstants.Paid)
            {
                return new PaymentResult(false, "Đơn hàng này đã được thanh toán.");
            }

            if (order.PaymentMethod != PaymentConstants.Online)
            {
                return new PaymentResult(false,
                    "Đơn hàng đang chọn thanh toán khi giao hàng. Hãy đổi sang thanh toán trực tuyến nếu muốn trả trước.");
            }

            if (order.TotalAmount <= 0)
            {
                return new PaymentResult(false, "Đơn hàng chưa có cước phí hợp lệ để thanh toán.");
            }

            // Cập nhật có điều kiện: chống bấm 2 lần / 2 tab cùng lúc.
            var now = DateTime.Now;
            var amount = order.TotalAmount;

            var affected = await _context.DeliveryOrders
                .Where(o => o.OrderCode == orderCode
                    && o.CustomerId == userId
                    && o.PaymentStatus != PaymentConstants.Paid
                    && o.PaymentMethod == PaymentConstants.Online
                    && o.CurrentStatus != CancelledStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, PaymentConstants.Paid)
                    .SetProperty(o => o.PaymentAmount, (decimal?)amount)
                    .SetProperty(o => o.PaymentTime, (DateTime?)now));

            return affected == 1
                ? new PaymentResult(true, $"Thanh toán thành công {amount:N0} VNĐ cho đơn {orderCode}.")
                : new PaymentResult(false, "Không thể thanh toán, trạng thái đơn hàng vừa thay đổi. Vui lòng tải lại trang.");
        }

        public async Task<PaymentResult> ChangeMethodAsync(string orderCode, string userId, string newMethod)
        {
            if (!PaymentConstants.Methods.Contains(newMethod))
            {
                return new PaymentResult(false, "Phương thức thanh toán không hợp lệ.");
            }

            var affected = await _context.DeliveryOrders
                .Where(o => o.OrderCode == orderCode
                    && o.CustomerId == userId
                    && o.CurrentStatus == NotReceivedStatus
                    && o.PaymentStatus != PaymentConstants.Paid)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentMethod, newMethod));

            return affected == 1
                ? new PaymentResult(true, $"Đã đổi phương thức thanh toán thành \"{newMethod}\".")
                : new PaymentResult(false,
                    "Chỉ đổi được phương thức khi đơn chưa được tiếp nhận và chưa thanh toán.");
        }

        public async Task<bool> CollectCodAsync(string orderCode)
        {
            var order = await _context.DeliveryOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null) return false;

            var now = DateTime.Now;
            var amount = order.TotalAmount;

            var affected = await _context.DeliveryOrders
                .Where(o => o.OrderCode == orderCode
                    && o.PaymentMethod == PaymentConstants.Cod
                    && o.PaymentStatus != PaymentConstants.Paid)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, PaymentConstants.Paid)
                    .SetProperty(o => o.PaymentAmount, (decimal?)amount)
                    .SetProperty(o => o.PaymentTime, (DateTime?)now));

            return affected == 1;
        }
    }
}
