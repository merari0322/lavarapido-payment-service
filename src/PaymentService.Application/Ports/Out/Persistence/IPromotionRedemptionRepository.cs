using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>Repository de los canjes (promotion.booking_promotion) y de las cifras que se derivan de ellos.</summary>
public interface IPromotionRedemptionRepository
{
    Task<bool> ExistsAsync(long bookingId, int promotionId, CancellationToken ct);

    /// <summary>Cuántas veces se ha canjeado la promoción en total.</summary>
    Task<int> CountAsync(int promotionId, CancellationToken ct);

    /// <summary>Cuántas veces la canjeó ese cliente (por created_by, ver PromotionRedemption.RedeemedBy).</summary>
    Task<int> CountByCustomerAsync(int promotionId, long customerUserId, CancellationToken ct);

    /// <summary>Suma de lo ya descontado por cupones en esa reserva.</summary>
    Task<decimal> AppliedDiscountTotalAsync(long bookingId, CancellationToken ct);

    /// <summary>Usos y ahorro total por promoción, para el listado y las métricas del admin.</summary>
    Task<IReadOnlyDictionary<int, RedemptionStats>> StatsByPromotionAsync(CancellationToken ct);

    void Add(PromotionRedemption redemption);
}

/// <summary>Usos y monto total descontado de una promoción.</summary>
public sealed record RedemptionStats(int Redemptions, decimal Savings)
{
    public static readonly RedemptionStats None = new(0, 0m);
}
