namespace PaymentService.Api.Contracts;

// Cuerpos JSON que reciben los endpoints de pagos. Son el contrato con la web y la app móvil, por
// eso viven en la API (no en Application) y conservan sus nombres de campo.

/// <summary>El cliente reporta un pago: reserva, cuenta a la que pagó, referencia e imagen del comprobante (data URL).</summary>
public sealed record ReportPaymentRequest(long BookingId, short PaymentAccountId, string? TransactionReference,
    string ReceiptImage);

/// <summary>El admin registra un pago recibido en el lavadero.</summary>
public sealed record ManualPaymentRequest(long BookingId, short PaymentAccountId, string? TransactionReference);

/// <summary>Motivo del rechazo (obligatorio, máximo 200 caracteres).</summary>
public sealed record RejectPaymentRequest(string Reason);

/// <summary>Crear o editar una cuenta del lavadero.</summary>
public sealed record SaveAccountRequest(string MethodCode, string AccountHolder, string? AccountNumber,
    string? QrImageUrl, string? Instructions, bool Active);
