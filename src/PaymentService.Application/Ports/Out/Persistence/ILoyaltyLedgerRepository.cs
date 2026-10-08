using PaymentService.Domain.Loyalty;

namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>Repository del ledger de puntos (payment.loyalty_transaction), append-only.</summary>
public interface ILoyaltyLedgerRepository
{
    /// <summary>Saldo actual: balance_after del último movimiento del cliente, o 0 si no tiene ninguno.</summary>
    Task<int> CurrentBalanceAsync(long customerId, CancellationToken ct);

    /// <summary>Si ya se acreditaron puntos EARNED por esa reserva (hace idempotente la acreditación).</summary>
    Task<bool> HasEarnedForBookingAsync(long customerId, long bookingId, CancellationToken ct);

    void Add(LoyaltyTransaction transaction);
}
