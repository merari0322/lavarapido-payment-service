namespace PaymentService.Domain.Common;

/// <summary>
/// Algo que ya ocurrio en el negocio (ej. "el pago fue aprobado").
/// Se nombra siempre en pasado.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
