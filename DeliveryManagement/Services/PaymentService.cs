using DeliveryManagement.Data;
using DeliveryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;

        public PaymentService(ApplicationDbContext context)
        {
            _context = context;
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
