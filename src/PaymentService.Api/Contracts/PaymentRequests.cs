namespace PaymentService.Api.Contracts;

// Cuerpos JSON que reciben los endpoints de pagos. Criterio de todo el servicio: la Api define
// solo cómo llegan los datos por HTTP (*Request) y los traduce a los Commands de Application; las
// respuestas son los *Dto de Application tal cual (ver Application/Payments/PaymentContracts.cs).

/// <summary>El cliente reporta un pago: reserva, cuenta a la que pagó, referencia e imagen del comprobante (data URL).</summary>
public sealed record ReportPaymentRequest(long BookingId, short PaymentAccountId, string? TransactionReference,
    string ReceiptImage);

/// <summary>El admin registra un pago recibido en el lavadero.</summary>
public sealed record ManualPaymentRequest(long BookingId, short PaymentAccountId, string? TransactionReference);

/// <summary>Motivo del rechazo (obligatorio, máximo 200 caracteres).</summary>
public sealed record RejectPaymentRequest(string Reason);
