using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Loyalty;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Acredita los puntos que gana una reserva cuando su pago queda aprobado (suma de
/// CatalogItem.loyaltyPoints de cada servicio, que booking-service expone como totalLoyaltyPoints).
/// Solo registra el movimiento en el ledger; lo confirma la Unit of Work del caso de uso que lo
/// llama, junto con la aprobación del pago.
/// </summary>
public sealed class LoyaltyRewardService
{
    private readonly ILoyaltyLedgerRepository _ledger;

    public LoyaltyRewardService(ILoyaltyLedgerRepository ledger)
    {
        _ledger = ledger;
    }

    /// <summary>
    /// Idempotente: si la reserva ya generó sus puntos (por ejemplo, una aprobación repetida tras
    /// un reembolso) no vuelve a acreditarlos. Sin dueño conocido o sin puntos, no hace nada.
    /// </summary>
    public async Task CreditForBookingAsync(BookingInfo booking, long actor, CancellationToken ct)
    {
        if (booking.OwnerUserId is not { } customerId || booking.TotalLoyaltyPoints <= 0) return;
        if (await _ledger.HasEarnedForBookingAsync(customerId, booking.Id, ct)) return;

        var balance = await _ledger.CurrentBalanceAsync(customerId, ct);
        _ledger.Add(LoyaltyTransaction.Earn(customerId, booking.Id, booking.TotalLoyaltyPoints, balance, actor));
    }
}
