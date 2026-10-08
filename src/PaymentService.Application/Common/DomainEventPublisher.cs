namespace PaymentService.Application.Common;

/// <summary>
/// Puerto hacia carwash.events (ADR-004/ADR-011), el mismo exchange de RabbitMQ que usan los
/// servicios en Java, con el mismo sobre {eventId, eventType, aggregateId, occurredAt, version,
/// payload}. notification-service lo escucha y convierte cada evento en notificaciones in-app y
/// de correo (EventNotificationFactory).
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(string eventType, string aggregateId, IReadOnlyDictionary<string, object?> payload,
        CancellationToken ct);
}
