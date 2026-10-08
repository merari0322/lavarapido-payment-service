using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Promotions;

/// <summary>
/// Traduce los eventos de dominio de una promoción al contrato de integración que escucha
/// notification-service (routing key payment.promotion_redeemed: aviso in-app y correo del canje).
/// </summary>
internal static class PromotionIntegrationEvents
{
    public static IEnumerable<IntegrationEvent> From(Promotion promotion) =>
        promotion.DomainEvents.OfType<PromotionRedeemed>().Select(Map).ToList();

    private static IntegrationEvent Map(PromotionRedeemed e) =>
        new("PromotionRedeemed", "payment.promotion_redeemed", e.BookingId.ToString(), e.OccurredOnUtc,
            new Dictionary<string, object?>
            {
                ["customerUserId"] = e.CustomerUserId,
                ["bookingId"] = e.BookingId,
                ["bookingCode"] = e.BookingCode,
                ["promotionCode"] = e.PromotionCode,
                ["promotionName"] = e.PromotionName,
                ["discountAmount"] = e.DiscountAmount
            });
}
