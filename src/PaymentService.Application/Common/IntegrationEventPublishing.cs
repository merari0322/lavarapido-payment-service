using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Common;

/// <summary>
/// Último paso común de todo caso de uso que cambia un aggregate: después de confirmar la Unit of
/// Work, publica los eventos de integración que salen de sus eventos de dominio y los descarta del
/// aggregate (para no publicarlos dos veces). Publicar antes de confirmar anunciaría algo que
/// quizá no quedó guardado.
/// </summary>
internal static class IntegrationEventPublishing
{
    public static async Task PublishAndClearAsync(this IIntegrationEventPublisher publisher,
        IAggregateRoot aggregate, IEnumerable<IntegrationEvent> integrationEvents, CancellationToken ct)
    {
        foreach (var integrationEvent in integrationEvents)
            await publisher.PublishAsync(integrationEvent, ct);
        aggregate.ClearDomainEvents();
    }
}
