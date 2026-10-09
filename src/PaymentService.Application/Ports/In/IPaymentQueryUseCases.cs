using PaymentService.Application.Payments;

namespace PaymentService.Application.Ports.In;

/// <summary>
/// Puerto de entrada: consultas de pagos. Separadas de los comandos (CQRS ligero) porque solo
/// leen y no necesitan la Unit of Work ni el bus de eventos. La visibilidad (qué reservas ve el
/// que llama) la decide booking-service con la identidad del request.
/// </summary>
public interface IPaymentQueryUseCases
{
    /// <summary>Pagos de las reservas del cliente que llama.</summary>
    Task<IReadOnlyList<PaymentDto>> MineAsync(CancellationToken ct);

    /// <summary>Un pago del cliente que llama (404 si la reserva no es suya).</summary>
    Task<PaymentDto> GetMineAsync(long paymentId, CancellationToken ct);

    /// <summary>Cada reserva del cliente que llama con el estado de su último pago y si se puede pagar.</summary>
    Task<IReadOnlyList<BookingPaymentStateDto>> MyBookingsAsync(CancellationToken ct);

    /// <summary>Cola de revisión del admin, opcionalmente filtrada por estado (PENDING, IN_REVIEW...).</summary>
    Task<IReadOnlyList<PaymentDto>> ListAsync(string? status, CancellationToken ct);

    /// <summary>Cualquier pago, para el admin.</summary>
    Task<PaymentDto> GetAsync(long paymentId, CancellationToken ct);

    /// <summary>Lo que falta por pagar de una reserva (el monto con el que quedaría un pago en caja).</summary>
    Task<AmountDueDto> AmountDueAsync(long bookingId, CancellationToken ct);
}
