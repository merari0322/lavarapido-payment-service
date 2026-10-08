using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Infrastructure.Persistence.Configurations;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Adaptador del puerto IUnitOfWork sobre EF Core. Todos los repositorios del request comparten el
/// mismo PaymentDbContext (Scoped), así que un solo SaveChanges confirma todo lo que registraron,
/// dentro de una transacción.
/// </summary>
internal sealed class EfUnitOfWork : IUnitOfWork
{
    // Errores de SQL Server por clave/índice único duplicado.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly PaymentDbContext _db;
    private readonly TimeProvider _clock;

    public EfUnitOfWork(PaymentDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        StampUpdatedAt();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Dos requests llegaron a la vez y la base (ux_payment_one_approved_per_booking,
            // uq_booking_promotion, uq_promotion_code...) frenó al segundo: es un conflicto, no un 500.
            throw new ConflictException(ErrorCodes.DataConflict,
                "La operación choca con otro cambio que se acaba de guardar. Recarga e intenta de nuevo.");
        }
    }

    /// <summary>Convención de auditoría: toda fila modificada registra cuándo (updated_at, UTC).</summary>
    private void StampUpdatedAt()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
            entry.Property(AuditColumns.UpdatedAt).CurrentValue = now;
    }
}
