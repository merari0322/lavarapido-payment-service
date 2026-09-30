using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Payments;
using PaymentService.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public async Task AddAsync(Payment payment, CancellationToken ct) =>
        await _db.Payments.AddAsync(payment, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}