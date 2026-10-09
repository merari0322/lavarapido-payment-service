using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador EF Core de IPaymentRepository. Siempre carga el pago con sus comprobantes (aggregate completo).</summary>
internal sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;

    public PaymentRepository(PaymentDbContext db)
    {
        _db = db;
    }

    private IQueryable<Payment> WithReceipts => _db.Payments.Include(p => p.Receipts);

    public Task<Payment?> GetByIdAsync(long id, CancellationToken ct) =>
        WithReceipts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> HasApprovedPaymentAsync(long bookingId, CancellationToken ct) =>
        _db.Payments.AnyAsync(p => p.BookingId == bookingId && p.Status == PaymentStatus.Approved, ct);

    public Task<bool> HasOpenPaymentAsync(long bookingId, CancellationToken ct) =>
        _db.Payments.AnyAsync(p => p.BookingId == bookingId
            && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.InReview), ct);

    // Usa el índice ix_payment_receipt_reference.
    public Task<bool> IsTransactionReferenceUsedElsewhereAsync(string transactionReference, long bookingId,
        CancellationToken ct) =>
        _db.Payments.AnyAsync(p => p.BookingId != bookingId
            && p.Receipts.Any(r => r.TransactionReference == transactionReference), ct);

    public async Task<IReadOnlyList<Payment>> ListAsync(PaymentStatus? status, int take, CancellationToken ct) =>
        await WithReceipts
            .Where(p => status == null || p.Status == status)
            .OrderByDescending(p => p.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListForBookingsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken ct) =>
        await WithReceipts
            .Where(p => bookingIds.Contains(p.BookingId))
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

    public void Add(Payment payment) => _db.Payments.Add(payment);
}
