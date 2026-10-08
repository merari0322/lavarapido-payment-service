namespace PaymentService.Application.Ports.Out.Integration;

/// <summary>
/// Puerto hacia el bus de eventos carwash.events (ADR-004/ADR-011), el mismo exchange de RabbitMQ
/// que usan los servicios en Java. notification-service lo escucha y convierte cada evento en
/// notificaciones in-app y de correo. Publicar es "mejor esfuerzo": si el bus falla, el caso de
/// uso ya confirmado no se deshace (el adaptador solo deja el error en el log).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct);
}

/// <summary>
/// Evento que sale del servicio. El adaptador lo envuelve en el sobre común
/// {eventId, eventType, aggregateId, occurredAt, version, payload}.
/// </summary>
/// <param name="EventType">Nombre que entiende el consumidor (PaymentConfirmed, PaymentRejected...).</param>
/// <param name="RoutingKey">Clave de ruteo del topic exchange (payment.confirmed...).</param>
/// <param name="AggregateId">Id de la entidad a la que se refiere.</param>
/// <param name="Payload">Datos del evento (camelCase, como los servicios en Java).</param>
public sealed record IntegrationEvent(
    string EventType,
    string RoutingKey,
    string AggregateId,
    IReadOnlyDictionary<string, object?> Payload);
