using PaymentService.Domain.Common;
using PaymentService.Domain.Payments;
using Xunit;

namespace PaymentService.Domain.UnitTests.Payments;
public class PaymentTests
{
    private const string Url = "https://archivos/comprobante1.jpg";

    private static Payment PendingPayment() =>
        Payment.Create(bookingId: 10, paymentAccountId: 1, amount: 150.50m);

    private static Payment InReviewPayment()
    {
        var payment = PendingPayment();
        payment.AttachReceipt(Url, uploadedBy: 5);
        payment.SubmitForReview();
        return payment;
    }

    private static Payment ApprovedPayment()
    {
        var payment = InReviewPayment();
        payment.Approve(approvedBy: 9);
        return payment;
    }

    // ---- Crear ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithZeroOrNegativeAmount_ThrowsDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => Payment.Create(1, 1, amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithZeroOrNegativeBookingId_ThrowsDomainException(long bookingId)
    {
        Assert.Throws<DomainException>(() => Payment.Create(bookingId, 1, 100m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithZeroOrNegativePaymentAccountId_ThrowsDomainException(int accountId)
    {
        Assert.Throws<DomainException>(() => Payment.Create(1, (short)accountId, 100m));
    }

    [Fact]
    public void Create_WithValidData_StartsPendingAndKeepsData()
    {
        var payment = Payment.Create(bookingId: 10, paymentAccountId: 2, amount: 150.50m);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(10, payment.BookingId);
        Assert.Equal(2, payment.PaymentAccountId);
        Assert.Equal(150.50m, payment.Amount);
    }

    // ---- Comprobantes ----

    [Fact]
    public void AttachReceipt_WhenPending_AddsTheReceipt()
    {
        var payment = PendingPayment();

        payment.AttachReceipt(Url, uploadedBy: 5, transactionReference: "REF123", reportedAmount: 150.50m);

        var receipt = Assert.Single(payment.Receipts);
        Assert.Equal(Url, receipt.FileUrl);
        Assert.Equal(5, receipt.UploadedBy);
        Assert.Equal("REF123", receipt.TransactionReference);
        Assert.Equal(150.50m, receipt.ReportedAmount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AttachReceipt_WithEmptyFileUrl_ThrowsDomainException(string? fileUrl)
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.AttachReceipt(fileUrl!, uploadedBy: 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AttachReceipt_WithInvalidUploader_ThrowsDomainException(long uploadedBy)
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.AttachReceipt(Url, uploadedBy));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AttachReceipt_WithZeroOrNegativeReportedAmount_ThrowsDomainException(double amount)
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() =>
            payment.AttachReceipt(Url, uploadedBy: 5, reportedAmount: (decimal)amount));
    }

    [Fact]
    public void AttachReceipt_WhenNotPending_ThrowsDomainException()
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.AttachReceipt("https://archivos/otro.jpg", uploadedBy: 5));
    }

    // ---- Enviar a revisión ----

    [Fact]
    public void SubmitForReview_WithReceipt_MovesToInReview()
    {
        var payment = PendingPayment();
        payment.AttachReceipt(Url, uploadedBy: 5);

        payment.SubmitForReview();

        Assert.Equal(PaymentStatus.InReview, payment.Status);
    }

    [Fact]
    public void SubmitForReview_WithoutReceipt_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.SubmitForReview());
    }

    // ---- Aprobar ----

    [Fact]
    public void Approve_WhenInReview_MovesToApproved()
    {
        var payment = InReviewPayment();

        payment.Approve(approvedBy: 9);

        Assert.Equal(PaymentStatus.Approved, payment.Status);
    }

    [Fact]
    public void Approve_WhenPending_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.Approve(approvedBy: 9));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Approve_WithInvalidApprover_ThrowsDomainException(long approvedBy)
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.Approve(approvedBy));
    }

    [Fact]
    public void Approve_StoresApproverAndProcessedAt()
    {
        var payment = InReviewPayment();

        payment.Approve(approvedBy: 9);

        Assert.Equal(9, payment.ApprovedBy);
        Assert.NotNull(payment.ProcessedAtUtc);
    }

    // ---- Rechazar ----

    [Fact]
    public void Reject_WithReason_MovesToRejected()
    {
        var payment = InReviewPayment();

        payment.Reject("El comprobante no es legible.");

        Assert.Equal(PaymentStatus.Rejected, payment.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Reject_WithoutReason_ThrowsDomainException(string? reason)
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.Reject(reason!));
    }

    [Fact]
    public void Reject_WithTooLongReason_ThrowsDomainException()
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.Reject(new string('x', 201)));
    }

    [Fact]
    public void Reject_WhenPending_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.Reject("Motivo válido."));
    }

    [Fact]
    public void Reject_StoresReasonAndProcessedAt()
    {
        var payment = InReviewPayment();

        payment.Reject("El comprobante no es legible.");

        Assert.Equal("El comprobante no es legible.", payment.RejectionReason);
        Assert.NotNull(payment.ProcessedAtUtc);
    }

    // ---- Reembolsar ----

    [Fact]
    public void Refund_WhenApproved_MovesToRefunded()
    {
        var payment = ApprovedPayment();

        payment.Refund();

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Refund_WhenNotApproved_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.Refund());
    }

    // ---- Eventos de dominio ----

    [Fact]
    public void Approve_RaisesPaymentApprovedEvent()
    {
        var payment = InReviewPayment();

        payment.Approve(approvedBy: 9);

        Assert.Contains(payment.DomainEvents, e => e is PaymentApproved);
    }

    [Fact]
    public void Reject_RaisesPaymentRejectedEvent()
    {
        var payment = InReviewPayment();

        payment.Reject("Motivo válido.");

        Assert.Contains(payment.DomainEvents, e => e is PaymentRejected);
    }

    [Fact]
    public void Refund_RaisesPaymentRefundedEvent()
    {
        var payment = ApprovedPayment();

        payment.Refund();

        Assert.Contains(payment.DomainEvents, e => e is PaymentRefunded);
    }
}