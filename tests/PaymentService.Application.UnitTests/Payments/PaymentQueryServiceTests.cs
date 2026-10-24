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

    // ------------------------------------------------------------ E-10: cola del admin por páginas

    private void FivePaymentsOnFiveBookings()
    {
        for (long id = 1; id <= 5; id++)
        {
            AddBooking(100 + id, "CONFIRMED");
            AddPayment(Reported(100 + id));
        }
    }

    [Fact]
    public async Task ListPage_ReturnsThatPageMostRecentFirstWithTheTotals()
    {
        FivePaymentsOnFiveBookings();

        var page = await _service.ListPageAsync(null, 1, 2, default);

        Assert.Equal(new long[] { 3, 2 }, page.Items.Select(p => p.Id));
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.Size);
        Assert.Equal(5, page.TotalElements);
        Assert.Equal(3, page.TotalPages);
        Assert.All(page.Items, p => Assert.NotNull(p.Booking));
    }

    [Fact]
    public async Task ListPage_DoesNotSendTheReceiptImageButSaysThereIsOne()
    {
        AddPayment(Reported(BookingId));

        var item = Assert.Single((await _service.ListPageAsync(null, 0, 20, default)).Items);
        var detail = await _service.GetAsync(item.Id, default);

        Assert.Null(item.ReceiptImage);
        Assert.True(item.HasReceiptImage);
        Assert.Equal("data:image/png;base64,AAA", detail.ReceiptImage);
    }

    [Fact]
    public async Task List_WithoutPaging_KeepsTheImageForTheCurrentScreens()
    {
        AddPayment(Reported(BookingId));

        var item = Assert.Single(await _service.ListAsync(null, default));

        Assert.Equal("data:image/png;base64,AAA", item.ReceiptImage);
        Assert.True(item.HasReceiptImage);
    }

    [Fact]
    public async Task ListPage_OnlyLooksUpTheBookingsOfThatPage()
    {
        FivePaymentsOnFiveBookings();

        await _service.ListPageAsync(null, 0, 2, default);

        Assert.Equal(2, _bookings.AdminLookups);
    }

    [Fact]
    public async Task ListPage_FiltersByStatusAndKeepsThePageWithinLimits()
    {
        FivePaymentsOnFiveBookings();
        var approved = Reported(BookingId);
        approved.Approve(1, FixedClock.Now);
        AddPayment(approved);

        var onlyApproved = await _service.ListPageAsync("approved", 0, 20, default);
        var tooSmall = await _service.ListPageAsync(null, -3, 0, default);
        var tooBig = await _service.ListPageAsync(null, 0, 500, default);

        Assert.Equal("APPROVED", Assert.Single(onlyApproved.Items).Status);
        Assert.Equal((0, 1), (tooSmall.Page, tooSmall.Size));
        Assert.Equal(PaymentQueryService.MaxPageSize, tooBig.Size);
        Assert.Equal(6, tooBig.Items.Count);
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

    // ---- Historial del cliente (RF-015) ----

    private void ThreeBookingsWithPayments()
    {
        _bookings.Bookings[8] = new BookingInfo(8, "RES-8", "COMPLETED", 40_000m, "2026-10-01", "09:00",
            "Polichado", "Mazda 3", "ABC123", 3, 0);
        _bookings.Bookings[9] = new BookingInfo(9, "RES-9", "COMPLETED", 30_000m, "2026-10-12", "15:00",
            "Lavado, Encerado", "Mazda 3", "ABC123", 3, 0);
        foreach (var bookingId in new long[] { BookingId, 8, 9 })
            _payments.Add(Payment.ReportWithReceipt(bookingId, 1, 10_000m, "data:image/png;base64,AAA", 3, null, DateTime.UtcNow));
        _uow.CommitAsync(default).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Mine_IsOrderedFromTheMostRecentBooking()
    {
        ThreeBookingsWithPayments();

        var history = await _service.MineAsync(PaymentHistoryFilter.None, default);

        Assert.Equal(new[] { "RES-9", "RES-7", "RES-8" }, history.Select(p => p.Booking!.Code));
    }

    [Fact]
    public async Task Mine_FiltersByDatesAndService()
    {
        ThreeBookingsWithPayments();

        var october = await _service.MineAsync(new PaymentHistoryFilter(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 31), null), default);
        var washes = await _service.MineAsync(new PaymentHistoryFilter(null, null, " lavado "), default);

        Assert.Equal(new[] { "RES-9", "RES-7" }, october.Select(p => p.Booking!.Code));
        Assert.Equal(new[] { "RES-9", "RES-7" }, washes.Select(p => p.Booking!.Code));
    }

    [Fact]
    public async Task Mine_WithTheDatesBackwards_IsAnInvalidRequest()
    {
        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _service.MineAsync(new PaymentHistoryFilter(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1), null), default));

        Assert.Equal(ErrorCodes.InvalidDateRange, error.Code);
    }
}
