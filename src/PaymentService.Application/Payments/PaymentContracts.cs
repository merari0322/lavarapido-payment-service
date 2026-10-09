using PaymentService.Application.PaymentAccounts;

namespace PaymentService.Application.Payments;

// Contrato de los casos de uso de pagos. Criterio de todo el servicio:
//   - Application define lo que entra a un caso de uso (*Command) y lo que sale (*Dto). Los Dto se
//     serializan tal cual en las respuestas HTTP, así que sus nombres de campo son contrato con la
//     web y la app móvil.
//   - La Api define solo los cuerpos de los requests (*Request), porque cómo llega un dato por HTTP
//     (opcional, con valor por defecto, en la ruta...) es asunto del adaptador; luego los traduce a
//     Commands.

/// <summary>
/// Pago listo para mostrar: sus datos, el último comprobante, la cuenta y la reserva.
/// Amount es lo que se espera cobrar; ReportedAmount, lo que el comprobante dice que se pagó (null
/// si el cliente no lo indicó): el admin compara ambos antes de aprobar.
/// </summary>
public sealed record PaymentDto(
    long Id,
    string Status,
    decimal Amount,
    DateTime? ProcessedAtUtc,
    string? RejectionReason,
    string? TransactionReference,
    decimal? ReportedAmount,
    string? ReceiptImage,
    DateTime? ReportedAtUtc,
    long? ReportedBy,
    PaymentAccountDto? Account,
    PaymentBookingDto? Booking);

/// <summary>
/// Lo que falta por pagar de una reserva: su total, lo ya descontado por cupones y la diferencia,
/// que es exactamente el monto con el que se registra el pago.
/// </summary>
public sealed record AmountDueDto(
    long BookingId,
    decimal BookingTotal,
    decimal AppliedDiscounts,
    decimal AmountDue);

/// <summary>
/// Cómo va el pago de una reserva del cliente: el estado de su último pago (null si no tiene) y si
/// todavía se puede pagar. Payable lo decide PaymentPolicy, así la web y la app solo muestran
/// "Pagar" cuando payment-service aceptaría el reporte.
/// </summary>
public sealed record BookingPaymentStateDto(
    long BookingId,
    string? PaymentStatus,
    bool Payable);

/// <summary>Los datos de la reserva que se muestran junto al pago.</summary>
public sealed record PaymentBookingDto(
    long Id,
    string Code,
    string Status,
    decimal Total,
    string Date,
    string StartTime,
    string Services,
    string Vehicle,
    string Plate,
    long? OwnerUserId);

/// <summary>
/// El cliente reporta que pagó por QR: reserva, cuenta a la que pagó, referencia, imagen del
/// comprobante y, si lo indica, el monto que dice el comprobante.
/// </summary>
public sealed record ReportPaymentCommand(
    long BookingId,
    short PaymentAccountId,
    string? TransactionReference,
    string ReceiptImage,
    decimal? ReportedAmount = null);

/// <summary>El admin registra un pago recibido en el lavadero.</summary>
public sealed record RegisterInPersonPaymentCommand(
    long BookingId,
    short PaymentAccountId,
    string? TransactionReference);
