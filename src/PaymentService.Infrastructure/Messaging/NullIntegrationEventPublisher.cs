using PaymentService.Application.Ports.Out.Integration;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Patrón Null Object: se registra cuando MESSAGING_ENABLED no está en "true". Cumple el puerto sin
/// publicar nada, así los casos de uso no necesitan preguntar si hay bus de eventos.
/// </summary>
internal sealed class NullIntegrationEventPublisher : IIntegrationEventPublisher
{
    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct) => Task.CompletedTask;
}
