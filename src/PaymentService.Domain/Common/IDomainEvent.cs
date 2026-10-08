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
/// Base de los eventos de dominio como records inmutables: fija la fecha en que ocurrieron para
/// que cada evento concreto solo declare sus propios datos.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
