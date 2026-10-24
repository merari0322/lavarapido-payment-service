using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions;

/// <summary>
/// Una promoción aplicada a una reserva y cuánto descontó.
/// AppliedAmount es lo que descontó el día del canje: si la promoción cambia o vence después, ese
/// dato histórico no se toca. Lo que descuenta HOY lo da DiscountOn: un cupón porcentual guarda su
/// porcentaje y se recalcula con el total actual de la reserva (BIZ-05: si la reserva se reprograma
/// con otros servicios, el 10 % sigue siendo el 10 % y no un monto en pesos que puede dejarla en 0).
/// Solo se crea desde Promotion.Redeem, que es quien valida las reglas del canje.
/// </summary>
public sealed class PromotionRedemption : Entity<long>
{
    private PromotionRedemption() { }

    public long BookingId { get; private set; }
    public int PromotionId { get; private set; }
    public decimal AppliedAmount { get; private set; }

    /// <summary>
    /// Porcentaje del cupón al canjearlo (null si es de monto fijo o si el canje es de antes de que
    /// se guardara: entonces descuenta AppliedAmount).
    /// </summary>
    public decimal? DiscountPercent { get; private set; }

    /// <summary>Tope en pesos que tenía el cupón porcentual al canjearlo (null = sin tope).</summary>
    public decimal? MaxDiscountAmount { get; private set; }

    /// <summary>
    /// Cliente que canjeó: es el dato que permite contar los canjes por cliente para
    /// MaxRedemptionsPerCustomer. Es opcional solo por los canjes históricos que no lo registraron;
    /// todo canje creado por este servicio lo llena.
    /// </summary>
    public long? RedeemedBy { get; private set; }

    public DateTime RedeemedAtUtc { get; private set; }

    internal static PromotionRedemption Create(long bookingId, int promotionId, decimal appliedAmount, long redeemedBy,
        DateTime redeemedAtUtc, decimal? discountPercent = null, decimal? maxDiscountAmount = null)
    {
        Guard.PositiveId(bookingId, DomainErrorCodes.InvalidRedemptionBooking,
            "El canje debe estar asociado a una reserva válida.");
        Guard.Against(appliedAmount <= 0, DomainErrorCodes.PromotionNothingToDiscount,
            "La reserva no tiene saldo sobre el cual descontar.");

        return new PromotionRedemption
        {
            BookingId = bookingId,
            PromotionId = promotionId,
            AppliedAmount = appliedAmount,
            DiscountPercent = discountPercent,
            MaxDiscountAmount = maxDiscountAmount,
            RedeemedBy = redeemedBy,
            RedeemedAtUtc = redeemedAtUtc
        };
    }

    /// <summary>
    /// Lo que descuenta hoy sobre ese subtotal (lo que falta por pagar de la reserva): el porcentaje
    /// guardado o, si no hay, lo que se congeló al canjear. Con los mismos topes del canje: nunca deja
    /// la reserva en 0 (E-07), aunque el total haya bajado por debajo del descuento.
    /// </summary>
    public decimal DiscountOn(decimal subtotal)
    {
        var discount = DiscountPercent is { } percent ? subtotal * percent / 100m : AppliedAmount;
        return Promotion.ApplyCaps(discount, subtotal, MaxDiscountAmount);
    }

    /// <summary>
    /// Lo que descuenta hoy cada canje de una reserva sobre su total actual, en el orden en que se
    /// hicieron: cada uno se calcula sobre lo que dejó el anterior, igual que al canjearlos. Con E-09
    /// hay a lo sumo uno; el orden importa solo para datos de antes.
    /// </summary>
    public static IReadOnlyList<RedemptionDiscount> DiscountsOn(decimal bookingTotal,
        IEnumerable<PromotionRedemption> redemptionsInOrder)
    {
        var subtotal = bookingTotal;
        var discounts = new List<RedemptionDiscount>();
        foreach (var redemption in redemptionsInOrder)
        {
            var discount = redemption.DiscountOn(subtotal);
            discounts.Add(new RedemptionDiscount(redemption, discount));
            subtotal -= discount;
        }
        return discounts;
    }
}

/// <summary>Un canje y lo que descuenta hoy.</summary>
public sealed record RedemptionDiscount(PromotionRedemption Redemption, decimal Amount);
