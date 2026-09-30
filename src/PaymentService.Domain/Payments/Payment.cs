using PaymentService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Domain.Payments;

public sealed class Payment : AggregateRoot<long>
{
    private const int MaxRejectionReasonLength = 200;

    private readonly List<PaymentReceipt> _receipts = new();

    private Payment() { }

    public long BookingId { get; private set; }
    public short PaymentAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public long? ApprovedBy { get; private set; }
    public string? RejectionReason { get; private set; }

    public IReadOnlyCollection<PaymentReceipt> Receipts => _receipts.AsReadOnly();

    public static Payment Create(long bookingId, short paymentAccountId, decimal amount)
    {
        if (bookingId <= 0)
            throw new DomainException("El pago debe estar asociado a una reserva válida.");

        if (paymentAccountId <= 0)
            throw new DomainException("El pago debe indicar la cuenta de pago.");

        if (amount <= 0)
            throw new DomainException("El monto del pago debe ser mayor que cero.");

        return new Payment
        {
            BookingId = bookingId,
            PaymentAccountId = paymentAccountId,
            Amount = amount,
            Status = PaymentStatus.Pending
        };
    }

    public void AttachReceipt(
        string fileUrl, long uploadedBy, string? transactionReference = null, decimal? reportedAmount = null)
    {
        EnsureStatus(PaymentStatus.Pending, "Solo se puede adjuntar un comprobante a un pago pendiente.");

        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new DomainException("El comprobante debe tener un archivo.");

        if (uploadedBy <= 0)
            throw new DomainException("El comprobante debe indicar quién lo subió.");

        if (reportedAmount is <= 0)
            throw new DomainException("El monto reportado en el comprobante debe ser mayor que cero.");

        _receipts.Add(PaymentReceipt.Create(fileUrl, uploadedBy, transactionReference, reportedAmount));
    }

    public void SubmitForReview()
    {
        EnsureStatus(PaymentStatus.Pending, "Solo un pago pendiente puede enviarse a revisión.");

        if (_receipts.Count == 0)
            throw new DomainException("Para enviar a revisión se necesita al menos un comprobante.");

        Status = PaymentStatus.InReview;
    }

    public void Approve(long approvedBy)
    {
        EnsureStatus(PaymentStatus.InReview, "Solo un pago en revisión puede aprobarse.");

        if (approvedBy <= 0)
            throw new DomainException("La aprobación debe indicar quién aprueba.");

        Status = PaymentStatus.Approved;
        ApprovedBy = approvedBy;
        ProcessedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new PaymentApproved(Id));
    }

    public void Reject(string reason)
    {
        EnsureStatus(PaymentStatus.InReview, "Solo un pago en revisión puede rechazarse.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Para rechazar un pago hay que indicar el motivo.");

        if (reason.Length > MaxRejectionReasonLength)
            throw new DomainException($"El motivo no puede superar {MaxRejectionReasonLength} caracteres.");

        Status = PaymentStatus.Rejected;
        RejectionReason = reason;
        ProcessedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new PaymentRejected(Id, reason));
    }

    public void Refund()
    {
        EnsureStatus(PaymentStatus.Approved, "Solo un pago aprobado puede reembolsarse.");

        Status = PaymentStatus.Refunded;
        AddDomainEvent(new PaymentRefunded(Id));
    }

    private void EnsureStatus(PaymentStatus expected, string message)
    {
        if (Status != expected)
            throw new DomainException(message);
    }
}