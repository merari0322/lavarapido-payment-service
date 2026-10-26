using PaymentService.Domain.Common;

namespace PaymentService.Domain.Payments;

// Eventos de dominio del aggregate Payment. No llevan el PaymentId porque un pago nuevo todavía no
// tiene Id cuando ocurre el evento (se asigna al guardarlo); quien los publica los lee junto con el
// aggregate ya guardado (ver PaymentIntegrationEvents en Application).

/// <summary>
/// El cliente reportó el pago con su comprobante y quedó en revisión (también cuando lo vuelve a
/// reportar tras un rechazo: es un pago nuevo). Para booking-service la reserva ya no vence por falta de pago.
/// </summary>
public sealed record PaymentReported(long BookingId, decimal Amount, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

/// <summary>El pago quedó aprobado: la reserva está pagada.</summary>
public sealed record PaymentApproved(long BookingId, decimal Amount, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

/// <summary>El pago fue rechazado por el motivo indicado; el cliente puede intentarlo de nuevo.</summary>
public sealed record PaymentRejected(long BookingId, decimal Amount, string Reason, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

/// <summary>Un pago aprobado se devolvió al cliente.</summary>
public sealed record PaymentRefunded(long BookingId, decimal Amount, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);
