namespace PaymentService.Application.Loyalty;

/// <summary>
/// Puerto hacia payment.loyalty_transaction (el ledger) y promotion.booking_promotion (los
/// canjes). customerId es el id de usuario (security.app_user), ver nota en LoyaltyTransactionRecord.
/// </summary>
public interface ILoyaltyRepository
{
    /// <summary>Saldo actual: balance_after de la última fila del cliente, o 0 si no tiene ninguna.</summary>
    Task<int> CurrentBalanceAsync(long customerId, CancellationToken ct);

    /// <summary>
    /// Acredita los puntos ganados por una reserva pagada. No hace nada si ya existe una fila
    /// EARNED para ese booking (idempotente ante una aprobación repetida).
    /// </summary>
    Task<bool> CreditEarnedAsync(long customerId, long bookingId, int points, long? actor, CancellationToken ct);

    Task<bool> AlreadyRedeemedAsync(long bookingId, int promotionId, CancellationToken ct);

    Task RecordRedemptionAsync(long bookingId, int promotionId, decimal appliedAmount, long actor, CancellationToken ct);

    /// <summary>Suma de lo ya descontado por cupones canjeados en esta reserva (se resta del total al pagar).</summary>
    Task<decimal> AppliedDiscountsAsync(long bookingId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
