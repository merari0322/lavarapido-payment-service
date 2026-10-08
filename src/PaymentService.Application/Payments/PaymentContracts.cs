using PaymentService.Application.PaymentAccounts;

namespace PaymentService.Application.Payments;

// Contrato de los casos de uso de pagos. Criterio de todo el servicio:
//   - Application define lo que entra a un caso de uso (*Command) y lo que sale (*Dto). Los Dto se
//     serializan tal cual en las respuestas HTTP, así que sus nombres de campo son contrato con la
//     web y la app móvil.
//   - La Api define solo los cuerpos de los requests (*Request), porque cómo llega un dato por HTTP
//     (opcional, con valor por defecto, en la ruta...) es asunto del adaptador; luego los traduce a
//     Commands.

/// <summary>Pago listo para mostrar: sus datos, el último comprobante, la cuenta y la reserva.</summary>
public sealed record PaymentDto(
    long Id,
    string Status,
    decimal Amount,
    DateTime? ProcessedAtUtc,
    string? RejectionReason,
    string? TransactionReference,
    string? ReceiptImage,
    DateTime? ReportedAtUtc,
    long? ReportedBy,
    PaymentAccountDto? Account,
    PaymentBookingDto? Booking);

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

/// <summary>El cliente reporta que pagó por QR: reserva, cuenta a la que pagó, referencia e imagen del comprobante.</summary>
public sealed record ReportPaymentCommand(
    long BookingId,
    short PaymentAccountId,
    string? TransactionReference,
    string ReceiptImage);

/// <summary>El admin registra un pago recibido en el lavadero.</summary>
public sealed record RegisterInPersonPaymentCommand(
    long BookingId,
    short PaymentAccountId,
    string? TransactionReference);
