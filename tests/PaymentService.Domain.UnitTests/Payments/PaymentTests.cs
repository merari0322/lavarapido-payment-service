using PaymentService.Domain.Common;
using PaymentService.Domain.Payments;
using Xunit;

namespace PaymentService.Domain.UnitTests.Payments;

public class PaymentTests
{
    private const string Url = "https://archivos/comprobante1.jpg";
    private const long Admin = 9;
    private static readonly DateTime Now = new(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);

    private static Payment PendingPayment() =>
        Payment.Create(bookingId: 10, paymentAccountId: 1, amount: 150.50m);

    private static Payment InReviewPayment()
    {
        var payment = PendingPayment();
        payment.AttachReceipt(Url, uploadedBy: 5, nowUtc: Now);
        payment.SubmitForReview();
        return payment;
    }

    private static Payment ApprovedPayment()
    {
        var payment = InReviewPayment();
        payment.Approve(Admin, Now);
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

    // ---- Factory methods ----

    [Fact]
    public void ReportWithReceipt_LeavesPaymentInReviewWithItsReceipt()
    {
        var payment = Payment.ReportWithReceipt(10, 2, 80m, "data:image/png;base64,AAA", reportedBy: 5, " REF-1 ", Now);

        Assert.Equal(PaymentStatus.InReview, payment.Status);
        var receipt = Assert.Single(payment.Receipts);
        Assert.Equal("REF-1", receipt.TransactionReference);
        Assert.Equal(80m, receipt.ReportedAmount);
        Assert.True(receipt.HasImage);
    }

    [Fact]
    public void ReportWithReceipt_WhenFileIsNotAnImage_ThrowsDomainException()
    {
        var error = Assert.Throws<DomainException>(() =>
            Payment.ReportWithReceipt(10, 2, 80m, "no-es-imagen", reportedBy: 5, null, Now));

        Assert.Equal("INVALID_RECEIPT_FILE", error.Code);
    }

    [Fact]
    public void RegisterInPerson_IsApprovedByTheAdminWithoutImage()
    {
        var payment = Payment.RegisterInPerson(10, 1, 80m, registeredBy: Admin, transactionReference: null, nowUtc: Now);

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(Admin, payment.ApprovedBy);
        Assert.False(payment.LatestReceipt!.HasImage);
        Assert.Contains(payment.DomainEvents, e => e is PaymentApproved);
    }

    // ---- Comprobantes ----

    [Fact]
    public void AttachReceipt_WhenPending_AddsTheReceipt()
    {
        var payment = PendingPayment();

        payment.AttachReceipt(Url, uploadedBy: 5, nowUtc: Now, transactionReference: "REF123", reportedAmount: 150.50m);

        var receipt = Assert.Single(payment.Receipts);
        Assert.Equal(Url, receipt.FileUrl);
        Assert.Equal(5, receipt.UploadedBy);
        Assert.Equal("REF123", receipt.TransactionReference);
        Assert.Equal(150.50m, receipt.ReportedAmount);
        Assert.Null(receipt.ReviewedBy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AttachReceipt_WithEmptyFileUrl_ThrowsDomainException(string? fileUrl)
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.AttachReceipt(fileUrl!, uploadedBy: 5, nowUtc: Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AttachReceipt_WithInvalidUploader_ThrowsDomainException(long uploadedBy)
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.AttachReceipt(Url, uploadedBy, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AttachReceipt_WithZeroOrNegativeReportedAmount_ThrowsDomainException(double amount)
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() =>
            payment.AttachReceipt(Url, uploadedBy: 5, nowUtc: Now, reportedAmount: (decimal)amount));
    }

    [Fact]
    public void AttachReceipt_WithTooLongReference_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() =>
            payment.AttachReceipt(Url, uploadedBy: 5, nowUtc: Now, transactionReference: new string('x', 101)));
    }

    [Fact]
    public void AttachReceipt_WhenNotPending_ThrowsDomainException()
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.AttachReceipt("https://archivos/otro.jpg", uploadedBy: 5, nowUtc: Now));
    }

    // ---- Enviar a revisión ----

    [Fact]
    public void SubmitForReview_WithReceipt_MovesToInReview()
    {
        var payment = PendingPayment();
        payment.AttachReceipt(Url, uploadedBy: 5, nowUtc: Now);

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

        payment.Approve(Admin, Now);

        Assert.Equal(PaymentStatus.Approved, payment.Status);
    }

    [Fact]
    public void Approve_WhenPending_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.Approve(Admin, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Approve_WithInvalidApprover_ThrowsDomainException(long approvedBy)
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.Approve(approvedBy, Now));
    }

    [Fact]
    public void Approve_StoresApproverAndProcessedAt()
    {
        var payment = InReviewPayment();

        payment.Approve(Admin, Now);

        Assert.Equal(Admin, payment.ApprovedBy);
        Assert.NotNull(payment.ProcessedAtUtc);
    }

    [Fact]
    public void Approve_MarksTheReceiptAsReviewed()
    {
        var payment = InReviewPayment();

        payment.Approve(Admin, Now);

        var receipt = payment.LatestReceipt!;
        Assert.Equal(Admin, receipt.ReviewedBy);
        Assert.NotNull(receipt.ReviewedAtUtc);
    }

    // ---- Rechazar ----

    [Fact]
    public void Reject_WithReason_MovesToRejected()
    {
        var payment = InReviewPayment();

        payment.Reject(Admin, "El comprobante no es legible.", Now);

        Assert.Equal(PaymentStatus.Rejected, payment.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Reject_WithoutReason_ThrowsDomainException(string? reason)
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.Reject(Admin, reason!, Now));
    }

    [Fact]
    public void Reject_WithTooLongReason_ThrowsDomainException()
    {
        var payment = InReviewPayment();

        Assert.Throws<DomainException>(() => payment.Reject(Admin, new string('x', 201), Now));
    }

    [Fact]
    public void Reject_WhenPending_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.Reject(Admin, "Motivo válido.", Now));
    }

    [Fact]
    public void Reject_StoresReasonAndReviewOnTheReceipt()
    {
        var payment = InReviewPayment();

        payment.Reject(Admin, "  El comprobante no es legible.  ", Now);

        Assert.Equal("El comprobante no es legible.", payment.RejectionReason);
        Assert.NotNull(payment.ProcessedAtUtc);
        Assert.Null(payment.ApprovedBy);
        Assert.Equal(Admin, payment.LatestReceipt!.ReviewedBy);
        Assert.Equal("El comprobante no es legible.", payment.LatestReceipt.ReviewComment);
    }

    // ---- Reembolsar ----

    [Fact]
    public void Refund_WhenApproved_MovesToRefunded()
    {
        var payment = ApprovedPayment();

        payment.Refund(Now);

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void Refund_WhenNotApproved_ThrowsDomainException()
    {
        var payment = PendingPayment();

        Assert.Throws<DomainException>(() => payment.Refund(Now));
    }

    // ---- Eventos de dominio ----

    [Fact]
    public void Approve_RaisesPaymentApprovedEventWithBookingAndAmount()
    {
        var payment = InReviewPayment();

        payment.Approve(Admin, Now);

        var approved = Assert.IsType<PaymentApproved>(Assert.Single(payment.DomainEvents));
        Assert.Equal(10, approved.BookingId);
        Assert.Equal(150.50m, approved.Amount);
    }

    [Fact]
    public void Reject_RaisesPaymentRejectedEventWithReason()
    {
        var payment = InReviewPayment();

        payment.Reject(Admin, "Motivo válido.", Now);

        var rejected = Assert.IsType<PaymentRejected>(Assert.Single(payment.DomainEvents));
        Assert.Equal("Motivo válido.", rejected.Reason);
    }

    [Fact]
    public void Refund_RaisesPaymentRefundedEvent()
    {
        var payment = ApprovedPayment();

        payment.Refund(Now);

        Assert.Contains(payment.DomainEvents, e => e is PaymentRefunded);
    }

    // ---- Códigos de estado ----

    [Theory]
    [InlineData("pending", PaymentStatus.Pending)]
    [InlineData(" IN_REVIEW ", PaymentStatus.InReview)]
    [InlineData("Approved", PaymentStatus.Approved)]
    public void StatusCodes_ParseIgnoresCaseAndSpaces(string code, PaymentStatus expected)
    {
        Assert.Equal(expected, PaymentStatusCodes.Parse(code));
    }

    [Fact]
    public void StatusCodes_ParseUnknownCode_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => PaymentStatusCodes.Parse("PAGADO"));
    }

    // ---- Fechas (el aggregate usa el "ahora" que recibe, nunca el reloj del sistema) ----

    [Fact]
    public void Approve_UsesTheGivenTimeForProcessingReviewAndEvent()
    {
        var payment = InReviewPayment();

        payment.Approve(Admin, Now);

        Assert.Equal(Now, payment.ProcessedAtUtc);
        Assert.Equal(Now, payment.LatestReceipt!.ReviewedAtUtc);
        Assert.Equal(Now, Assert.Single(payment.DomainEvents).OccurredOnUtc);
    }

    [Fact]
    public void ReportWithReceipt_StampsTheReceiptWithTheGivenTime()
    {
        var payment = Payment.ReportWithReceipt(10, 2, 80m, "data:image/png;base64,AAA", reportedBy: 5, null, Now);

        Assert.Equal(Now, payment.LatestReceipt!.UploadedAtUtc);
    }
}
