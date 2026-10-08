using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Loyalty;
using PaymentService.Domain.Common;

namespace PaymentService.Infrastructure.Persistence;

public class LoyaltyRepository : ILoyaltyRepository
{
    private readonly PaymentDbContext _db;

    public LoyaltyRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<int> CurrentBalanceAsync(long customerId, CancellationToken ct)
    {
        var last = await _db.LoyaltyTransactions
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
        return last?.BalanceAfter ?? 0;
    }

    public async Task<bool> CreditEarnedAsync(long customerId, long bookingId, int points, long? actor, CancellationToken ct)
    {
        if (points <= 0) return false;
        var alreadyCredited = await _db.LoyaltyTransactions
            .AnyAsync(t => t.CustomerId == customerId && t.BookingId == bookingId, ct);
        if (alreadyCredited) return false;

        var earnedTypeId = await MovementTypeIdAsync("EARNED", ct);
        var balance = await CurrentBalanceAsync(customerId, ct);
        var transaction = LoyaltyTransactionRecord.Create(customerId, earnedTypeId, bookingId, points,
            balance + points, "Puntos ganados por reserva pagada");
        await _db.LoyaltyTransactions.AddAsync(transaction, ct);
        SetCreatedBy(transaction, actor);
        return true;
    }

    public Task<bool> AlreadyRedeemedAsync(long bookingId, int promotionId, CancellationToken ct) =>
        _db.BookingPromotions.AnyAsync(b => b.BookingId == bookingId && b.PromotionId == promotionId, ct);

    public async Task RecordRedemptionAsync(long bookingId, int promotionId, decimal appliedAmount, long actor, CancellationToken ct)
    {
        var record = BookingPromotionRecord.Create(bookingId, promotionId, appliedAmount, actor);
        await _db.BookingPromotions.AddAsync(record, ct);

        // puntos canjeados: se registran como movimiento informativo (sign -1), no se le resta
        // saldo al cliente porque los puntos "desbloquean" la promoción, no se gastan (RF-026).
    }

    public async Task<decimal> AppliedDiscountsAsync(long bookingId, CancellationToken ct) =>
        await _db.BookingPromotions.Where(b => b.BookingId == bookingId).SumAsync(b => b.AppliedAmount, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    private async Task<short> MovementTypeIdAsync(string code, CancellationToken ct)
    {
        var id = await _db.LoyaltyMovementTypes.Where(t => t.Code == code).Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (id == 0)
            throw new DomainException("LOYALTY_MOVEMENT_TYPE_MISSING", $"El tipo {code} no está en payment.loyalty_movement_type (migración 015).");
        return id;
    }

    private void SetCreatedBy(LoyaltyTransactionRecord transaction, long? actor)
    {
        if (actor is null) return;
        _db.Entry(transaction).Property("CreatedBy").CurrentValue = actor;
    }
}
