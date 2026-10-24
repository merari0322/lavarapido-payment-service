using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Puntos que acaba de ganar un cliente con una reserva pagada: cuántos, cómo quedó su saldo y qué
/// promociones desbloqueó con ellos (las que pedían más puntos de los que tenía antes y ya no).
/// Con esto se publica payment.loyalty_points_earned para que notification-service le avise.
/// </summary>
public sealed record LoyaltyCredit(
    long CustomerUserId,
    long BookingId,
    string BookingCode,
    int Points,
    int Balance,
    IReadOnlyList<Promotion> UnlockedPromotions);
