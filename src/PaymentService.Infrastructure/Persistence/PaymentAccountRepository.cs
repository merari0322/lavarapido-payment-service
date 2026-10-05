using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Payments;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public class PaymentAccountRepository : IPaymentAccountRepository
{
    private readonly PaymentDbContext _db;

    public PaymentAccountRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PaymentAccount>> ListAsync(bool onlyActive, CancellationToken ct) =>
        await _db.PaymentAccounts.Where(a => !onlyActive || a.IsActive).OrderBy(a => a.Id).ToListAsync(ct);

    public Task<PaymentAccount?> GetByIdAsync(short id, CancellationToken ct) =>
        _db.PaymentAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<PaymentMethodType>> MethodTypesAsync(CancellationToken ct) =>
        await _db.PaymentMethodTypes.AsNoTracking().OrderBy(m => m.Id).ToListAsync(ct);

    public async Task AddAsync(PaymentAccount account, CancellationToken ct) =>
        await _db.PaymentAccounts.AddAsync(account, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
