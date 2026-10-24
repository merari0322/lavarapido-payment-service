namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>
/// Patrón Unit of Work: los repositorios solo registran cambios (agregar, modificar) y este puerto
/// los confirma todos juntos en una sola transacción. Así, por ejemplo, aprobar un pago y acreditar
/// los puntos que ganó la reserva quedan guardados los dos o ninguno.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Guarda todos los cambios pendientes. Si choca con una restricción única de la base (por
    /// ejemplo, un segundo pago aprobado para la misma reserva) lanza ConflictException.
    /// </summary>
    Task CommitAsync(CancellationToken ct);
}
