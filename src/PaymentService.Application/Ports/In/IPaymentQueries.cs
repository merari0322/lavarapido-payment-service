using PaymentService.Application.Common;
using PaymentService.Application.Payments;

namespace PaymentService.Application.Ports.In;

/// <summary>
/// Puerto de entrada: consultas de pagos. Separadas de los comandos (CQRS ligero) porque solo
/// leen y no necesitan la Unit of Work ni el bus de eventos.
/// </summary>
public interface IPaymentQueries
{
    /// <summary>Pagos de las reservas del cliente que llama.</summary>
    Task<IReadOnlyList<PaymentView>> MineAsync(Caller caller, CancellationToken ct);

    /// <summary>Un pago del cliente que llama (404 si la reserva no es suya).</summary>
    Task<PaymentView> GetMineAsync(long paymentId, Caller caller, CancellationToken ct);

    /// <summary>Cola de revisión del admin, opcionalmente filtrada por estado (PENDING, IN_REVIEW...).</summary>
    Task<IReadOnlyList<PaymentView>> ListAsync(string? status, Caller admin, CancellationToken ct);

    /// <summary>Cualquier pago, para el admin.</summary>
    Task<PaymentView> GetAsync(long paymentId, Caller admin, CancellationToken ct);
}
