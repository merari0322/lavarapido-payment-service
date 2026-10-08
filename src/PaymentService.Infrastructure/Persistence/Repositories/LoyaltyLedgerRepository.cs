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

    // El último movimiento por Id (IDENTITY crece con el tiempo) tiene el saldo vigente.
    public async Task<int> CurrentBalanceAsync(long customerId, CancellationToken ct) =>
        await _db.LoyaltyTransactions
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.Id)
            .Select(t => (int?)t.BalanceAfter)
            .FirstOrDefaultAsync(ct) ?? 0;

    public Task<bool> HasEarnedForBookingAsync(long customerId, long bookingId, CancellationToken ct)
    {
        // Variable (no constante): EF la manda como parámetro y la convierte al ID real en cada
        // ejecución, en lugar de dejar un ID fijo en el SQL que guarda en caché (ver CatalogIds).
        var earned = LoyaltyMovementType.Earned;
        return _db.LoyaltyTransactions.AnyAsync(t => t.CustomerId == customerId
            && t.BookingId == bookingId
            && t.MovementType == earned, ct);
    }

    public void Add(LoyaltyTransaction transaction) => _db.LoyaltyTransactions.Add(transaction);
}
