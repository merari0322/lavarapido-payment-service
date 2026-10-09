using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Promotions;
using PaymentService.Infrastructure.Persistence.Configurations;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de IPromotionRepository.</summary>
internal sealed class PromotionRepository : IPromotionRepository
{
    private readonly PaymentDbContext _db;
    private readonly TimeProvider _clock;

    public PromotionRepository(PaymentDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Promotion>> ListAsync(CancellationToken ct) =>
        await _db.Promotions.OrderBy(p => p.Id).ToListAsync(ct);

    public Task<Promotion?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var normalized = Normalize(code);
        return _db.Promotions.FirstOrDefaultAsync(p => p.Code == normalized, ct);
    }

    public Task<bool> ExistsCodeAsync(string code, int? exceptId, CancellationToken ct)
    {
        var normalized = Normalize(code);
        return _db.Promotions.AnyAsync(p => p.Code == normalized && (exceptId == null || p.Id != exceptId), ct);
    }

    public void Add(Promotion promotion) => _db.Promotions.Add(promotion);

    // El borrado lógico es un detalle de persistencia: se marca en las columnas shadow de auditoría.
    public void Remove(Promotion promotion, long deletedBy)
    {
        var entry = _db.Entry(promotion);
        entry.Property(AuditColumns.DeletedAt).CurrentValue = _clock.GetUtcNow().UtcDateTime;
        entry.Property(AuditColumns.DeletedBy).CurrentValue = deletedBy;
    }

    // Los códigos se guardan en mayúscula (Promotion los normaliza al crearlos).
    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
