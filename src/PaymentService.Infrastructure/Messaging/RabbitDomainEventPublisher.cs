using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Common;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Publica en carwash.events (ADR-004) con el mismo sobre que usan los servicios en Java
/// ({eventId, eventType, aggregateId, occurredAt, version, payload}). payment-service no tenía
/// mensajería hasta el canje de cupones (RF de fidelización/promociones): si RabbitMQ está caído
/// el canje no falla, solo queda el error en el log, igual que en booking-service.
/// </summary>
public sealed class RabbitDomainEventPublisher : IDomainEventPublisher, IDisposable
{
    private const string Exchange = "carwash.events";
    private const int ContractVersion = 1;

    private readonly ILogger<RabbitDomainEventPublisher> _log;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;

    public RabbitDomainEventPublisher(ILogger<RabbitDomainEventPublisher> log, string host, int port,
        string username, string password)
    {
        _log = log;
        _factory = new ConnectionFactory { HostName = host, Port = port, UserName = username, Password = password };
    }

    public Task PublishAsync(string eventType, string aggregateId, IReadOnlyDictionary<string, object?> payload,
        CancellationToken ct)
    {
        var eventId = Guid.NewGuid().ToString();
        var envelope = new Dictionary<string, object?>
        {
            ["eventId"] = eventId,
            ["eventType"] = eventType,
            ["aggregateId"] = aggregateId,
            ["occurredAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["version"] = ContractVersion,
            ["payload"] = payload,
        };
        try
        {
            var connection = _connection ??= _factory.CreateConnection("payment-service");
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            var props = channel.CreateBasicProperties();
            props.ContentType = "application/json";
            props.ContentEncoding = "UTF-8";
            props.MessageId = eventId;
            props.DeliveryMode = 2; // persistent
            var routingKey = eventType switch
            {
                "PromotionRedeemed" => "payment.promotion_redeemed",
                _ => eventType.ToLowerInvariant(),
            };
            channel.BasicPublish(Exchange, routingKey, props, body);
            _log.LogInformation("Published {EventType} ({EventId}) for {AggregateId}", eventType, eventId, aggregateId);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Could not publish {EventType} ({EventId}) to RabbitMQ", eventType, eventId);
        }
        return Task.CompletedTask;
    }

    public void Dispose() => _connection?.Dispose();
}

/// <summary>
/// Se usa cuando MESSAGING_ENABLED no está en "true" (por defecto, igual que en los servicios en
/// Java con @ConditionalOnProperty): no publica nada, para no intentar conectar a RabbitMQ en los
/// entornos donde no está levantado.
/// </summary>
public sealed class NullDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync(string eventType, string aggregateId, IReadOnlyDictionary<string, object?> payload,
        CancellationToken ct) => Task.CompletedTask;
}
