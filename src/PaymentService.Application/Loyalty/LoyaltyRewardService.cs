using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Loyalty;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Mantiene los puntos que gana una reserva alineados con su pago: se acreditan cuando el pago
/// queda aprobado (suma de los puntos de cada servicio, que booking-service expone como
/// totalLoyaltyPoints) y se revierten cuando ese pago se reembolsa. Solo registra movimientos en
/// el ledger; los confirma la Unit of Work del caso de uso que lo llama, junto con el pago.
/// </summary>
public sealed class LoyaltyRewardService
{
    private readonly ILoyaltyLedgerRepository _ledger;
    private readonly IPromotionRepository _promotions;

    public LoyaltyRewardService(ILoyaltyLedgerRepository ledger, IPromotionRepository promotions)
    {
        _ledger = ledger;
        _promotions = promotions;
    }

    /// <summary>
    /// Idempotente: si la reserva ya tiene sus puntos vigentes para ese cliente no los vuelve a
    /// acreditar. Si se reembolsó y luego se pagó de nuevo, sus puntos netos son 0 y se acreditan
    /// otra vez. Sin dueño conocido o sin puntos, no hace nada.
    ///
    /// Devuelve lo acreditado y las promociones que esos puntos desbloquearon (null si no acreditó
    /// nada), para que el caso de uso publique el evento después de confirmar.
    /// </summary>
    public async Task<LoyaltyCredit?> CreditForBookingAsync(BookingInfo booking, long actor, DateTime nowUtc,
        CancellationToken ct)
    {
        if (booking.OwnerUserId is not { } customerId || booking.TotalLoyaltyPoints <= 0) return null;

        var current = await _ledger.NetPointsByCustomerForBookingAsync(booking.Id, ct);
        if (current.ContainsKey(customerId)) return null;

        var balance = await _ledger.CurrentBalanceAsync(customerId, ct);
        var earned = LoyaltyTransaction.Earn(customerId, booking.Id, booking.TotalLoyaltyPoints, balance, actor, nowUtc);
        _ledger.Add(earned);

        var today = DateOnly.FromDateTime(nowUtc);
        var unlocked = (await _promotions.ListAsync(ct))
            .Where(p => p.IsUnlockedBy(balance.Points, earned.BalanceAfter, today))
            .OrderBy(p => p.RequiredPoints)
            .ToList();
        return new LoyaltyCredit(customerId, booking.Id, booking.Code, earned.Points, earned.BalanceAfter, unlocked);
    }

    /// <summary>
    /// Revierte los puntos que la reserva todavía conserva. No necesita a booking-service: el
    /// ledger sabe a qué cliente se le acreditaron. Idempotente: sin puntos netos, no hace nada.
    /// </summary>
    public async Task RevokeForBookingAsync(long bookingId, long actor, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var (customerId, netPoints) in await _ledger.NetPointsByCustomerForBookingAsync(bookingId, ct))
        {
            var balance = await _ledger.CurrentBalanceAsync(customerId, ct);
            var reversal = LoyaltyTransaction.ReverseEarned(customerId, bookingId, netPoints, balance, actor, nowUtc);
            if (reversal is not null) _ledger.Add(reversal);
        }
    }
}
