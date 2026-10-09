using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Loyalty;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de ILoyaltyLedgerRepository (payment.loyalty_transaction).</summary>
internal sealed class LoyaltyLedgerRepository : ILoyaltyLedgerRepository
{
    private readonly PaymentDbContext _db;

    public LoyaltyLedgerRepository(PaymentDbContext db)
    {
        _db = db;
    }

    // El último movimiento del cliente (mayor sequence_no) tiene el saldo vigente.
    public async Task<LoyaltyBalance> CurrentBalanceAsync(long customerId, CancellationToken ct)
    {
        var last = await _db.LoyaltyTransactions
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.Sequence)
            .Select(t => new { t.BalanceAfter, t.Sequence })
            .FirstOrDefaultAsync(ct);
        return last is null ? LoyaltyBalance.Empty : new LoyaltyBalance(last.BalanceAfter, last.Sequence);
    }

    // Solo los movimientos ligados a una reserva (ganados y revertidos) llevan booking_id, así que
    // su suma por cliente es lo que esa reserva le dejó.
    public async Task<IReadOnlyDictionary<long, int>> NetPointsByCustomerForBookingAsync(long bookingId, CancellationToken ct)
    {
        var rows = await _db.LoyaltyTransactions
            .Where(t => t.BookingId == bookingId)
            .GroupBy(t => t.CustomerId)
            .Select(g => new { CustomerId = g.Key, Net = g.Sum(t => t.Points) })
            .Where(r => r.Net > 0)
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.CustomerId, r => r.Net);
    }

    public void Add(LoyaltyTransaction transaction) => _db.LoyaltyTransactions.Add(transaction);
}
