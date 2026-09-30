using PaymentService.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.Payments;
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> HasApprovedPaymentAsync(long bookingId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
