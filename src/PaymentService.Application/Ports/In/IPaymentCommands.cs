using PaymentService.Application.Common;
using PaymentService.Application.Payments;

namespace PaymentService.Application.Ports.In;

/// <summary>
/// Puerto de entrada (driving port): casos de uso que CAMBIAN el estado de un pago. Los
/// controllers dependen de esta interfaz, no de la implementación (inversión de dependencias).
/// </summary>
public interface IPaymentCommands
{
    /// <summary>El cliente reporta un pago por QR con su comprobante; queda en revisión.</summary>
    Task<PaymentView> ReportAsync(ReportPaymentCommand command, Caller caller, CancellationToken ct);

    /// <summary>El admin registra un pago recibido en el lavadero; queda aprobado.</summary>
    Task<PaymentView> RegisterInPersonAsync(RegisterInPersonPaymentCommand command, Caller admin, CancellationToken ct);

    /// <summary>El admin aprueba un pago en revisión y se acreditan los puntos de la reserva.</summary>
    Task<PaymentView> ApproveAsync(long paymentId, Caller admin, CancellationToken ct);

    /// <summary>El admin rechaza un pago en revisión indicando el motivo.</summary>
    Task<PaymentView> RejectAsync(long paymentId, string reason, Caller admin, CancellationToken ct);

    /// <summary>El admin registra la devolución de un pago aprobado.</summary>
    Task<PaymentView> RefundAsync(long paymentId, Caller admin, CancellationToken ct);
}
