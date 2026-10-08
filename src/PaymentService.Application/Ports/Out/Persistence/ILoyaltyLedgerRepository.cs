using PaymentService.Domain.Loyalty;

namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>Repository del ledger de puntos, append-only.</summary>
public interface ILoyaltyLedgerRepository
{
    /// <summary>Saldo y posición del último movimiento del cliente (LoyaltyBalance.Empty si no tiene ninguno).</summary>
    Task<LoyaltyBalance> CurrentBalanceAsync(long customerId, CancellationToken ct);

    /// <summary>
    /// Puntos netos que cada cliente conserva por esa reserva (ganados menos revertidos). Solo
    /// incluye clientes con saldo neto positivo; vacío si la reserva no tiene puntos vigentes.
    /// </summary>
    Task<IReadOnlyDictionary<long, int>> NetPointsByCustomerForBookingAsync(long bookingId, CancellationToken ct);

    void Add(LoyaltyTransaction transaction);
}
