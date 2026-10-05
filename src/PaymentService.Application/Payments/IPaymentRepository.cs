using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> HasApprovedPaymentAsync(long bookingId, CancellationToken ct);
    // un pago pendiente o en revisión: no se reporta otro para la misma reserva
    Task<bool> HasOpenPaymentAsync(long bookingId, CancellationToken ct);
    Task<IReadOnlyList<Payment>> ListAsync(PaymentStatus? status, int take, CancellationToken ct);
    Task<IReadOnlyList<Payment>> ListForBookingsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
