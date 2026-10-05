using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Payments;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;

    public PaymentRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public Task<Payment?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.Payments.Include(p => p.Receipts).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> HasApprovedPaymentAsync(long bookingId, CancellationToken ct) =>
        _db.Payments.AnyAsync(p => p.BookingId == bookingId && p.Status == PaymentStatus.Approved, ct);

    public Task<bool> HasOpenPaymentAsync(long bookingId, CancellationToken ct) =>
        _db.Payments.AnyAsync(p => p.BookingId == bookingId
            && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.InReview), ct);

    public async Task<IReadOnlyList<Payment>> ListAsync(PaymentStatus? status, int take, CancellationToken ct) =>
        await _db.Payments.Include(p => p.Receipts)
            .Where(p => status == null || p.Status == status)
            .OrderByDescending(p => p.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListForBookingsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken ct) =>
        await _db.Payments.Include(p => p.Receipts)
            .Where(p => bookingIds.Contains(p.BookingId))
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct) =>
        await _db.Payments.AddAsync(payment, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
