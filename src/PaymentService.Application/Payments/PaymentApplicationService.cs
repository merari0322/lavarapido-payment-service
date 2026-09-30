using PaymentService.Application.Common;
using PaymentService.Domain.Common;
using PaymentService.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.Payments;
public class PaymentApplicationService
{
    private readonly IPaymentRepository _payments;

    public PaymentApplicationService(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public async Task<long> RegisterPaymentAsync(
        long bookingId, short paymentAccountId, decimal amount, CancellationToken ct)
    {
        var payment = Payment.Create(bookingId, paymentAccountId, amount);

        await _payments.AddAsync(payment, ct);
        await _payments.SaveChangesAsync(ct);

        return payment.Id;
    }

    public async Task<PaymentDto> GetAsync(long paymentId, CancellationToken ct)
    {
        var p = await LoadAsync(paymentId, ct);

        return new PaymentDto(
            p.Id, p.BookingId, p.PaymentAccountId, p.Amount, p.Status.ToString(),
            p.ProcessedAtUtc, p.ApprovedBy, p.RejectionReason,
            p.Receipts
                .Select(r => new ReceiptDto(
                    r.Id, r.FileUrl, r.TransactionReference, r.ReportedAmount, r.UploadedBy, r.UploadedAtUtc))
                .ToList());
    }

    public Task AttachReceiptAsync(
        long paymentId, string fileUrl, long uploadedBy,
        string? transactionReference, decimal? reportedAmount, CancellationToken ct) =>
        ExecuteAsync(paymentId, p => p.AttachReceipt(fileUrl, uploadedBy, transactionReference, reportedAmount), ct);

    public Task SubmitForReviewAsync(long paymentId, CancellationToken ct) =>
        ExecuteAsync(paymentId, p => p.SubmitForReview(), ct);

    public async Task ApproveAsync(long paymentId, long approvedBy, CancellationToken ct)
    {
        var payment = await LoadAsync(paymentId, ct);

        // La base también lo impide (índice único), pero así el error es claro.
        if (await _payments.HasApprovedPaymentAsync(payment.BookingId, ct))
            throw new DomainException("La reserva ya tiene un pago aprobado.");

        payment.Approve(approvedBy);
        await _payments.SaveChangesAsync(ct);
    }

    public Task RejectAsync(long paymentId, string reason, CancellationToken ct) =>
        ExecuteAsync(paymentId, p => p.Reject(reason), ct);

    public Task RefundAsync(long paymentId, CancellationToken ct) =>
        ExecuteAsync(paymentId, p => p.Refund(), ct);

    private async Task<Payment> LoadAsync(long paymentId, CancellationToken ct) =>
        await _payments.GetByIdAsync(paymentId, ct)
            ?? throw new NotFoundException($"No existe el pago {paymentId}.");

    // Cargar -> aplicar la regla del dominio -> guardar.
    private async Task ExecuteAsync(long paymentId, Action<Payment> action, CancellationToken ct)
    {
        var payment = await LoadAsync(paymentId, ct);
        action(payment);
        await _payments.SaveChangesAsync(ct);
    }
}