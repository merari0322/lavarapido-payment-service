using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.PaymentAccounts;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de IPaymentAccountRepository.</summary>
internal sealed class PaymentAccountRepository : IPaymentAccountRepository
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

    // El catálogo no se modifica aquí: se lee sin seguimiento de cambios.
    public async Task<IReadOnlyList<PaymentMethodType>> ListMethodTypesAsync(CancellationToken ct) =>
        await _db.PaymentMethodTypes.AsNoTracking().OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).ToListAsync(ct);

    public Task<PaymentMethodType?> GetMethodTypeAsync(short id, CancellationToken ct) =>
        _db.PaymentMethodTypes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<PaymentMethodType?> GetMethodTypeByCodeAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.PaymentMethodTypes.AsNoTracking().FirstOrDefaultAsync(m => m.Code == normalized, ct);
    }

    public void Add(PaymentAccount account) => _db.PaymentAccounts.Add(account);
}
