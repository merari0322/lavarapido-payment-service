using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Ports.Out.Integration;

namespace PaymentService.Application.Payments;

/// <summary>
/// Pago listo para mostrar: sus datos, el último comprobante, la cuenta y la reserva. Es el JSON
/// que consumen la web y la app móvil, así que sus nombres de campo son contrato.
/// </summary>
public sealed record PaymentView(
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
    BookingInfo? Booking);

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
