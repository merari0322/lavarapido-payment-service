namespace PaymentService.Domain.Common;

/// <summary>
/// Algo que ya ocurrió en el negocio (por ejemplo, "el pago fue aprobado").
/// Se nombra siempre en pasado y es inmutable.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

/// <summary>
/// Base de los eventos de dominio como records inmutables. La fecha la pone el aggregate con el
/// "ahora" que recibe del caso de uso (nunca DateTime.UtcNow), así las pruebas la pueden fijar.
/// </summary>
public abstract record DomainEvent(DateTime OccurredOnUtc) : IDomainEvent;
