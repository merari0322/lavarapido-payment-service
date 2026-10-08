using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions;

/// <summary>
/// Una promoción aplicada a una reserva (tabla promotion.booking_promotion) y cuánto descontó.
/// AppliedAmount se congela al momento del canje: si la promoción cambia o vence después, lo que
/// se descontó ese día es un hecho histórico y no se recalcula.
/// Solo se crea desde Promotion.Redeem, que es quien valida las reglas del canje.
/// </summary>
public sealed class PromotionRedemption : Entity<long>
{
    private PromotionRedemption() { }

    public long BookingId { get; private set; }
    public int PromotionId { get; private set; }
    public decimal AppliedAmount { get; private set; }

    /// <summary>
    /// Quién canjeó (columna created_by). La tabla no tiene customer_id: este dato es el que permite
    /// contar los canjes por cliente para max_redemptions_per_customer. Es nullable solo porque la
    /// columna lo es; todo canje creado por este servicio lo llena.
    /// </summary>
    public long? RedeemedBy { get; private set; }

    public DateTime RedeemedAtUtc { get; private set; }

    internal static PromotionRedemption Create(long bookingId, int promotionId, decimal appliedAmount, long redeemedBy)
    {
        Guard.PositiveId(bookingId, "INVALID_REDEMPTION_BOOKING", "El canje debe estar asociado a una reserva válida.");
        Guard.Against(appliedAmount <= 0, "PROMOTION_NOTHING_TO_DISCOUNT", "La reserva no tiene saldo sobre el cual descontar.");

        return new PromotionRedemption
        {
            BookingId = bookingId,
            PromotionId = promotionId,
            AppliedAmount = appliedAmount,
            RedeemedBy = redeemedBy,
            RedeemedAtUtc = DateTime.UtcNow
        };
    }
}
