using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Ports.Out.Integration;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Adaptador del puerto IIntegrationEventPublisher sobre RabbitMQ. Publica en el topic exchange
/// carwash.events con el mismo sobre que los servicios en Java
/// ({eventId, eventType, aggregateId, occurredAt, version, payload}).
///
/// Si RabbitMQ está caído el caso de uso no falla (ya está confirmado en la base): solo queda el
/// error en el log, igual que en booking-service. Garantizar la entrega exigiría un Outbox
/// (pattern-guide.md), que este servicio todavía no tiene.
/// Es Singleton: comparte una conexión (costosa de abrir) y crea un canal liviano por mensaje.
/// </summary>
internal sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher, IDisposable
{
    private const string Exchange = "carwash.events";
    private const int ContractVersion = 1;

    private readonly ILogger<RabbitMqIntegrationEventPublisher> _log;
    private readonly ConnectionFactory _factory;
    private readonly object _connectionLock = new();
    private IConnection? _connection;

    public RabbitMqIntegrationEventPublisher(ILogger<RabbitMqIntegrationEventPublisher> log, RabbitMqOptions options)
    {
        _log = log;
        _factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password
        };
    }

    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        var eventId = Guid.NewGuid().ToString();
        var envelope = new Dictionary<string, object?>
        {
            ["eventId"] = eventId,
            ["eventType"] = integrationEvent.EventType,
            ["aggregateId"] = integrationEvent.AggregateId,
            ["occurredAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["version"] = ContractVersion,
            ["payload"] = integrationEvent.Payload
        };

        try
        {
            using var channel = Connection().CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);

            var properties = channel.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.ContentEncoding = "UTF-8";
            properties.MessageId = eventId;
            properties.DeliveryMode = 2; // persistente: sobrevive a un reinicio del broker

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            channel.BasicPublish(Exchange, integrationEvent.RoutingKey, properties, body);
            _log.LogInformation("Published {EventType} ({EventId}) for {AggregateId}",
                integrationEvent.EventType, eventId, integrationEvent.AggregateId);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Could not publish {EventType} ({EventId}) to RabbitMQ", integrationEvent.EventType, eventId);
        }
        return Task.CompletedTask;
    }

    // Abre la conexión la primera vez (o tras perderla); el lock evita abrir dos a la vez.
    private IConnection Connection()
    {
        lock (_connectionLock)
        {
            if (_connection is { IsOpen: true }) return _connection;
            _connection?.Dispose();
            _connection = _factory.CreateConnection("payment-service");
            return _connection;
        }
    }

    public void Dispose() => _connection?.Dispose();
}
