using System.Text.Json.Serialization;

namespace PaymentService.Api.Contracts;

// Cuerpos JSON que reciben los endpoints de pagos. Criterio de todo el servicio: la Api define
// solo cómo llegan los datos por HTTP (*Request) y los traduce a los Commands de Application; las
// respuestas son los *Dto de Application tal cual (ver Application/Payments/PaymentContracts.cs).
// BookingId y PaymentAccountId son obligatorios (BIZ-13): si faltan en el JSON la respuesta es 400
// VALIDATION_ERROR, en vez de leerlos como 0 y fallar más adelante con otro error.

/// <summary>
/// El cliente reporta un pago: reserva, cuenta a la que pagó, referencia, imagen del comprobante
/// (data URL) y, opcionalmente, el monto que figura en el comprobante.
/// </summary>
public sealed record ReportPaymentRequest(
    [property: JsonRequired] long BookingId,
    [property: JsonRequired] short PaymentAccountId,
    string? TransactionReference,
    string ReceiptImage,
    decimal? ReportedAmount = null);

/// <summary>El admin registra un pago recibido en el lavadero.</summary>
public sealed record ManualPaymentRequest(
    [property: JsonRequired] long BookingId,
    [property: JsonRequired] short PaymentAccountId,
    string? TransactionReference);

/// <summary>Motivo del rechazo (obligatorio, máximo 200 caracteres).</summary>
public sealed record RejectPaymentRequest(string Reason);
