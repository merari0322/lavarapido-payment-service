using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Promotions;
using PaymentService.Domain.Common;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence;

public class PromotionRepository : IPromotionRepository
{
    private readonly PaymentDbContext _db;

    public PromotionRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Promotion>> ListAsync(CancellationToken ct) =>
        await _db.Promotions.OrderBy(p => p.Id).ToListAsync(ct);

    public Task<Promotion?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpper();
        return _db.Promotions.FirstOrDefaultAsync(p => p.Code == normalized, ct);
    }

    public async Task<bool> ExistsCodeAsync(string code, int? exceptId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpper();
        return await _db.Promotions.AnyAsync(p => p.Code == normalized && (exceptId == null || p.Id != exceptId), ct);
    }

    public async Task AddAsync(Promotion promotion, CancellationToken ct)
    {
        await _db.Promotions.AddAsync(promotion, ct);

        await FillLegacyColumnsAsync(promotion, ct);
    }

    public async Task SyncDiscountColumnsAsync(Promotion promotion, CancellationToken ct) =>
        await FillLegacyColumnsAsync(promotion, ct);

    /// <summary>
    /// discount_type_id/min_purchase_amount/min_completed_booking/is_public son NOT NULL en la
    /// tabla por una regla de antes de esta pantalla (ver Promotion, en Domain); discount_value ya
    /// es DiscountPercent (propiedad real, PromotionConfiguration lo mapea directo), solo falta el
    /// tipo PERCENT y el resto de columnas en su valor neutro.
    /// </summary>
    private async Task FillLegacyColumnsAsync(Promotion promotion, CancellationToken ct)
    {
        var percentTypeId = await _db.DiscountTypes.Where(d => d.Code == "PERCENT").Select(d => d.Id).FirstOrDefaultAsync(ct);
        if (percentTypeId == 0)
            throw new DomainException("PERCENT_DISCOUNT_TYPE_MISSING", "El tipo PERCENT no está en promotion.discount_type (migración 015).");

        var entry = _db.Entry(promotion);
        entry.Property("DiscountTypeId").CurrentValue = percentTypeId;
        entry.Property("MinPurchaseAmount").CurrentValue = 0m;
        entry.Property("MinCompletedBooking").CurrentValue = 0;
        entry.Property("IsPublic").CurrentValue = true;
    }

    public Task SoftDeleteAsync(Promotion promotion, long actor, CancellationToken ct)
    {
        var entry = _db.Entry(promotion);
        entry.Property("DeletedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("DeletedBy").CurrentValue = actor;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyDictionary<int, (int Redemptions, decimal Savings)>> RedemptionStatsAsync(CancellationToken ct)
    {
        var rows = await _db.BookingPromotions
            .GroupBy(b => b.PromotionId)
            .Select(g => new { PromotionId = g.Key, Count = g.Count(), Total = g.Sum(b => b.AppliedAmount) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.PromotionId, r => (r.Count, r.Total));
    }
}
