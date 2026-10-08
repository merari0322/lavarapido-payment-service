using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Domain.Common;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Traduce los eventos de dominio de un pago al contrato de integración que escucha
/// notification-service (routing keys payment.confirmed / payment.rejected de cross-cutting.md §7;
/// EventNotificationFactory lee paymentId, customerUserId, amount y reason).
///
/// El dominio no sabe quién es el cliente (la reserva es de booking-service), por eso
/// customerUserId llega aparte. El PaymentId se toma del aggregate ya guardado, no del evento.
/// </summary>
internal static class PaymentIntegrationEvents
{
    public static IEnumerable<IntegrationEvent> From(Payment payment, long? customerUserId) =>
        payment.DomainEvents.Select(e => Map(payment, e, customerUserId)).OfType<IntegrationEvent>();

    private static IntegrationEvent? Map(Payment payment, IDomainEvent domainEvent, long? customerUserId) => domainEvent switch
    {
        PaymentApproved e => Create("PaymentConfirmed", "payment.confirmed", payment, customerUserId, e.BookingId, e.Amount, null),
        PaymentRejected e => Create("PaymentRejected", "payment.rejected", payment, customerUserId, e.BookingId, e.Amount, e.Reason),
        PaymentRefunded e => Create("PaymentRefunded", "payment.refunded", payment, customerUserId, e.BookingId, e.Amount, null),
        _ => null
    };

    private static IntegrationEvent Create(string eventType, string routingKey, Payment payment, long? customerUserId,
        long bookingId, decimal amount, string? reason)
    {
        var payload = new Dictionary<string, object?>
        {
            ["paymentId"] = payment.Id,
            ["bookingId"] = bookingId,
            ["customerUserId"] = customerUserId,
            ["amount"] = amount
        };
        if (reason is not null) payload["reason"] = reason;
        return new IntegrationEvent(eventType, routingKey, payment.Id.ToString(), payload);
    }
}
