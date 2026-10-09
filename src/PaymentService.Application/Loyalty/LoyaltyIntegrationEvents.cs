using PaymentService.Application.Ports.Out.Integration;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Contrato de integración de los puntos de fidelización (routing key payment.loyalty_points_earned).
/// notification-service avisa al cliente cuántos puntos ganó y, por cada promoción desbloqueada,
/// que ya tiene un cupón canjeable (in-app y correo).
/// </summary>
internal static class LoyaltyIntegrationEvents
{
    public static IntegrationEvent PointsEarned(LoyaltyCredit credit, DateTime occurredOnUtc) =>
        new("LoyaltyPointsEarned", "payment.loyalty_points_earned", credit.BookingId.ToString(), occurredOnUtc,
            new Dictionary<string, object?>
            {
                ["customerUserId"] = credit.CustomerUserId,
                ["bookingId"] = credit.BookingId,
                ["bookingCode"] = credit.BookingCode,
                ["points"] = credit.Points,
                ["balance"] = credit.Balance,
                ["unlockedPromotions"] = credit.UnlockedPromotions.Select(p => new Dictionary<string, object?>
                {
                    ["code"] = p.Code,
                    ["name"] = p.Name,
                    ["discountPercent"] = p.DiscountPercent,
                    ["requiredPoints"] = p.RequiredPoints
                }).ToList()
            });
}
