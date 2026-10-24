using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions;

/// <summary>
/// Un cliente canjeó la promoción en una de sus reservas. Lleva los datos que el aviso al cliente
/// necesita (código y nombre del cupón tal como estaban al canjear).
/// </summary>
public sealed record PromotionRedeemed(
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    long BookingId,
    string BookingCode,
    long CustomerUserId,
    decimal DiscountAmount,
    DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
