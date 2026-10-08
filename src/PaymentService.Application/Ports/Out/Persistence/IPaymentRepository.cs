using PaymentService.Domain.Payments;

namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>
/// Patrón Repository: colección de aggregates Payment (con sus comprobantes) que oculta cómo se
/// guardan. No confirma cambios: eso lo hace IUnitOfWork.
/// </summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(long id, CancellationToken ct);

    Task<bool> HasApprovedPaymentAsync(long bookingId, CancellationToken ct);

    /// <summary>Si la reserva tiene un pago pendiente o en revisión (no se reporta otro encima).</summary>
    Task<bool> HasOpenPaymentAsync(long bookingId, CancellationToken ct);

    /// <summary>
    /// Control antifraude: si esa referencia de transacción ya aparece en un comprobante de un pago
    /// de OTRA reserva (el mismo comprobante usado para pagar dos reservas distintas).
    /// </summary>
    Task<bool> IsTransactionReferenceUsedElsewhereAsync(string transactionReference, long bookingId, CancellationToken ct);

    /// <summary>Los más recientes primero, opcionalmente filtrados por estado.</summary>
    Task<IReadOnlyList<Payment>> ListAsync(PaymentStatus? status, int take, CancellationToken ct);

    Task<IReadOnlyList<Payment>> ListForBookingsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken ct);

    void Add(Payment payment);
}
