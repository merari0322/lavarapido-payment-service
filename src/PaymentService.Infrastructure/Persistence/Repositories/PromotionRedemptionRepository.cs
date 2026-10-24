using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de IPromotionRedemptionRepository (promotion.booking_promotion).</summary>
internal sealed class PromotionRedemptionRepository : IPromotionRedemptionRepository
{
    private readonly PaymentDbContext _db;

    public PromotionRedemptionRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(long bookingId, int promotionId, CancellationToken ct) =>
        _db.PromotionRedemptions.AnyAsync(r => r.BookingId == bookingId && r.PromotionId == promotionId, ct);

    public Task<PromotionRedemption?> GetAsync(long bookingId, int promotionId, CancellationToken ct) =>
        _db.PromotionRedemptions.FirstOrDefaultAsync(r => r.BookingId == bookingId && r.PromotionId == promotionId, ct);

    public Task<bool> HasOtherOnBookingAsync(long bookingId, int promotionId, CancellationToken ct) =>
        _db.PromotionRedemptions.AnyAsync(r => r.BookingId == bookingId && r.PromotionId != promotionId, ct);

    public async Task<IReadOnlyList<PromotionRedemption>> ListForBookingAsync(long bookingId, CancellationToken ct) =>
        await _db.PromotionRedemptions.Where(r => r.BookingId == bookingId).OrderBy(r => r.Id).ToListAsync(ct);

    public Task<int> CountAsync(int promotionId, CancellationToken ct) =>
        _db.PromotionRedemptions.CountAsync(r => r.PromotionId == promotionId, ct);

    public Task<int> CountByCustomerAsync(int promotionId, long customerUserId, CancellationToken ct) =>
        _db.PromotionRedemptions.CountAsync(r => r.PromotionId == promotionId && r.RedeemedBy == customerUserId, ct);

    public async Task<IReadOnlyDictionary<int, RedemptionStats>> StatsByPromotionAsync(CancellationToken ct)
    {
        // Se agrupa en la base: solo viaja una fila por promoción.
        var rows = await _db.PromotionRedemptions
            .GroupBy(r => r.PromotionId)
            .Select(g => new { PromotionId = g.Key, Count = g.Count(), Total = g.Sum(r => r.AppliedAmount) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.PromotionId, r => new RedemptionStats(r.Count, r.Total));
    }

    public void Add(PromotionRedemption redemption) => _db.PromotionRedemptions.Add(redemption);

    public void Remove(PromotionRedemption redemption) => _db.PromotionRedemptions.Remove(redemption);
}
