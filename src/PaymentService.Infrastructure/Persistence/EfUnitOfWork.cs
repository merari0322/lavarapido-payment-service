using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Common;
using PaymentService.Infrastructure.Persistence.Configurations;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Adaptador del puerto IUnitOfWork sobre EF Core. Todos los repositorios del request comparten el
/// mismo PaymentDbContext (Scoped), así que un solo SaveChanges confirma todo lo que registraron,
/// dentro de una transacción.
///
/// Antes de guardar aplica las convenciones de auditoría y concurrencia de AuditColumns:
///   - toda fila modificada registra updated_at;
///   - toda fila nueva nace con row_version = 1 y cada UPDATE lo incrementa (concurrencia optimista);
///   - un aggregate con eventos de dominio pendientes cuenta como modificado aunque sus datos no
///     cambien (p. ej. una promoción que consumió un uso): así dos operaciones simultáneas sobre el
///     mismo aggregate chocan en vez de saltarse sus reglas (límites de canje, una sola aprobación).
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
        ApplyAuditAndConcurrency();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otro request cambió la misma fila entre que se leyó y se guardó.
            throw Conflict();
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Dos requests llegaron a la vez y la base (ux_payment_one_approved_per_booking,
            // uq_booking_promotion, ux_loyalty_transaction_customer_sequence...) frenó al segundo.
            throw Conflict();
        }
    }

    private void ApplyAuditAndConcurrency()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var entries = _db.ChangeTracker.Entries().ToList();

        foreach (var entry in entries.Where(e => e.State == EntityState.Modified))
            entry.Property(AuditColumns.UpdatedAt).CurrentValue = now;

        foreach (var entry in entries.Where(e => e.State == EntityState.Unchanged && e.Entity is IAggregateRoot { DomainEvents.Count: > 0 }))
            BumpRowVersion(entry);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added) entry.Property(AuditColumns.RowVersion).CurrentValue = 1;
            else if (entry.State == EntityState.Modified && !entry.Property(AuditColumns.RowVersion).IsModified) BumpRowVersion(entry);
        }
    }

    // El valor original queda en el WHERE (concurrencia); el nuevo se escribe en la fila.
    private static void BumpRowVersion(EntityEntry entry)
    {
        var rowVersion = entry.Property(AuditColumns.RowVersion);
        rowVersion.CurrentValue = (int)rowVersion.OriginalValue! + 1;
    }

    private static ConflictException Conflict() =>
        new(ErrorCodes.DataConflict, "La operación choca con otro cambio que se acaba de guardar. Recarga e intenta de nuevo.");
}
