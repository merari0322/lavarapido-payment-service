using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.UnitTests.Fakes;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;
using Xunit;

namespace PaymentService.Application.UnitTests.Payments;

public class PaymentQueryServiceTests
{
    private const long BookingId = 7;

    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeRedemptionRepository _redemptions;
    private readonly FakePaymentRepository _payments;
    private readonly FakeBookingDirectory _bookings = new();
    private readonly PaymentQueryService _service;

    public PaymentQueryServiceTests()
    {
        _redemptions = new FakeRedemptionRepository(_uow);
        _payments = new FakePaymentRepository(_uow);
        _service = new PaymentQueryService(_payments, _bookings,
            new PaymentDtoAssembler(new PaymentAccountReader(new FakePaymentAccountRepository())),
            new AmountDueCalculator(_redemptions));

        _bookings.Bookings[BookingId] = new BookingInfo(BookingId, "RES-7", "CONFIRMED", 50_000m, "2026-10-08", "10:00",
            "Lavado", "Mazda 3", "ABC123", 3, 25);
    }

    [Fact]
    public async Task AmountDue_SubtractsTheRedeemedCouponsFromTheBookingTotal()
    {
        _redemptions.Saved.Add(Ids.Create<PromotionRedemption>(("BookingId", BookingId), ("AppliedAmount", 12_500m)));

        var due = await _service.AmountDueAsync(BookingId, default);

        Assert.Equal(new AmountDueDto(BookingId, 50_000m, 12_500m, 37_500m), due);
    }

    [Fact]
    public async Task AmountDue_WithoutCoupons_IsTheBookingTotal()
    {
        var due = await _service.AmountDueAsync(BookingId, default);

        Assert.Equal(50_000m, due.AmountDue);
        Assert.Equal(0m, due.AppliedDiscounts);
    }

    [Fact]
    public async Task AmountDue_ForAnUnknownBooking_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => _service.AmountDueAsync(99, default));

        Assert.Equal(ErrorCodes.BookingNotFound, error.Code);
    }

    // ------------------------------------------------------------ estado de pago por reserva

    [Fact]
    public async Task MyBookings_WithoutPayments_ArePayableWhileTheBookingIsActive()
    {
        AddBooking(8, "CANCELLED");

        var states = await _service.MyBookingsAsync(default);

        Assert.Contains(new BookingPaymentStateDto(BookingId, null, true), states);
        Assert.Contains(new BookingPaymentStateDto(8, null, false), states);
    }

    [Fact]
    public async Task MyBookings_WithAPaymentInReview_ShowTheStatusAndAreNotPayable()
    {
        AddPayment(Reported(BookingId));

        var state = Assert.Single(await _service.MyBookingsAsync(default));

        Assert.Equal(new BookingPaymentStateDto(BookingId, "IN_REVIEW", false), state);
    }

    [Fact]
    public async Task MyBookings_AfterARejectedPayment_ArePayableAgain()
    {
        var rejected = Reported(BookingId);
        rejected.Reject(1, "Comprobante ilegible", FixedClock.Now);
        AddPayment(rejected);

        var state = Assert.Single(await _service.MyBookingsAsync(default));

        Assert.Equal(new BookingPaymentStateDto(BookingId, "REJECTED", true), state);
    }

    [Fact]
    public async Task MyBookings_ShowTheLatestPayment_ButAnApprovedOneStillBlocksPaying()
    {
        var approved = Reported(BookingId);
        approved.Approve(1, FixedClock.Now);
        AddPayment(approved);
        var rejectedLater = Reported(BookingId);
        rejectedLater.Reject(1, "Duplicado", FixedClock.Now);
        AddPayment(rejectedLater);

        var state = Assert.Single(await _service.MyBookingsAsync(default));

        Assert.Equal(new BookingPaymentStateDto(BookingId, "REJECTED", false), state);
    }

    [Fact]
    public async Task MyBookings_WithoutBookings_IsEmpty()
    {
        _bookings.Bookings.Clear();

        Assert.Empty(await _service.MyBookingsAsync(default));
    }

    private void AddBooking(long id, string status) =>
        _bookings.Bookings[id] = new BookingInfo(id, $"RES-{id}", status, 20_000m, "2026-10-08", "11:00",
            "Lavado", "Mazda 3", "ABC123", 3, 10);

    private static Payment Reported(long bookingId) =>
        Payment.ReportWithReceipt(bookingId, 1, 50_000m, "data:image/png;base64,AAA", 3, null, FixedClock.Now);

    // el id lo pone la persistencia: los más nuevos tienen id mayor
    private void AddPayment(Payment payment) =>
        _payments.Saved.Add(payment.WithId<Payment, long>(_payments.Saved.Count + 1));
}
