using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Promotions;

public interface IPromotionRepository
{
    Task<IReadOnlyList<Promotion>> ListAsync(CancellationToken ct);

    Task<Promotion?> GetByIdAsync(int id, CancellationToken ct);

    Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct);

    Task<bool> ExistsCodeAsync(string code, int? exceptId, CancellationToken ct);

    Task AddAsync(Promotion promotion, CancellationToken ct);

    /// <summary>Re-sincroniza discount_type_id/discount_value (shadow) con el DiscountPercent actual tras un Update.</summary>
    Task SyncDiscountColumnsAsync(Promotion promotion, CancellationToken ct);

    /// <summary>No se borra de la tabla (queda el histórico de booking_promotion): se marca deleted_at/by.</summary>
    Task SoftDeleteAsync(Promotion promotion, long actor, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Cuántas veces se aplicó cada promoción y cuánto descontó en total (promotion.booking_promotion).</summary>
    Task<IReadOnlyDictionary<int, (int Redemptions, decimal Savings)>> RedemptionStatsAsync(CancellationToken ct);
}
