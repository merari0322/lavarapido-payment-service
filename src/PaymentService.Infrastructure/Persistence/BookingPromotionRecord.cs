namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Una fila de promotion.booking_promotion: qué promoción se aplicó a qué reserva y cuánto
/// descontó. La escribe LoyaltyApplicationService.RedeemAsync cuando el cliente canjea un cupón
/// en el pago; el índice único (booking_id, promotion_id) impide canjear la misma promo dos veces
/// en la misma reserva.
/// </summary>
public class BookingPromotionRecord
{
    public long Id { get; private set; }
    public long BookingId { get; private set; }
    public int PromotionId { get; private set; }
    public decimal AppliedAmount { get; private set; }

    private BookingPromotionRecord() { }

    public static BookingPromotionRecord Create(long bookingId, int promotionId, decimal appliedAmount, long actor)
    {
        var record = new BookingPromotionRecord
        {
            BookingId = bookingId,
            PromotionId = promotionId,
            AppliedAmount = appliedAmount,
        };
        record.CreatedBy = actor;
        return record;
    }

    public long? CreatedBy { get; private set; }
}
