using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Promotions;
using PaymentService.Application.UnitTests.Fakes;
using PaymentService.Domain.Promotions;
using Xunit;

namespace PaymentService.Application.UnitTests.Promotions;

public class CustomerPromotionServiceTests
{
    private const long BookingId = 7;
    private static readonly Caller Customer = new(3);

    private readonly FakeUnitOfWork _uow = new();
    private readonly FakePromotionRepository _promotions = new();
    private readonly FakeRedemptionRepository _redemptions;
    private readonly FakePaymentRepository _payments;
    private readonly FakeBookingDirectory _bookings = new();
    private readonly RecordingPublisher _events = new();
    private readonly CustomerPromotionService _service;

    public CustomerPromotionServiceTests()
    {
        _redemptions = new FakeRedemptionRepository(_uow);
        _payments = new FakePaymentRepository(_uow);
        _service = new CustomerPromotionService(_promotions, _redemptions, new FakeLedger(_uow), _bookings,
            new BookingPaymentGuard(_payments, new FakePaymentAccountRepository()), new AmountDueCalculator(_redemptions),
            _uow, _events, new FixedClock());

        _bookings.Bookings[BookingId] = new BookingInfo(BookingId, "RES-7", "CONFIRMED", 40_000m, "2026-10-08", "10:00",
            "Lavado", "Mazda 3", "ABC123", Customer.UserId, 0);

        var today = DateOnly.FromDateTime(FixedClock.Now);
        _promotions.Add(Promotion.Create(new PromotionDefinition("VERANO10", "Verano", null, 50_000m, 60, null, false,
                Array.Empty<string>(), today.AddDays(-1), today.AddDays(30), DiscountType.Percentage, 10m, 0))
            .WithId<Promotion, int>(1));
        _promotions.Add(Promotion.Create(new PromotionDefinition("OTRO5", "Otro", null, null, null, null, false,
                Array.Empty<string>(), today.AddDays(-1), today.AddDays(30), DiscountType.Percentage, 5m, 0))
            .WithId<Promotion, int>(2));
    }

    [Fact]
    public async Task Redeem_SavesTheDiscountAndPublishesTheEventFromTheAggregate()
    {
        var result = await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "verano10"), Customer, default);

        Assert.Equal(4_000m, result.DiscountAmount);
        Assert.Equal(36_000m, result.NewTotal);
        Assert.Single(_redemptions.Saved);

        var published = Assert.Single(_events.Published);
        Assert.Equal("payment.promotion_redeemed", published.RoutingKey);
        Assert.Equal("RES-7", published.Payload["bookingCode"]);
        Assert.Equal(FixedClock.Now, published.OccurredOnUtc);
        Assert.Empty(_promotions.Promotions[0].DomainEvents);
    }

    [Fact]
    public async Task Redeem_TheSameCouponTwiceOnABooking_IsRejectedByTheDomain()
    {
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);

        var error = await Assert.ThrowsAsync<Domain.Common.DomainException>(() =>
            _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default));

        Assert.Equal("PROMOTION_ALREADY_REDEEMED", error.Code);
        Assert.Single(_events.Published);
    }

    [Fact]
    public async Task Redeem_ASecondDifferentCouponOnTheSameBooking_IsRejected()
    {
        // E-09: una sola promoción por reserva
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);

        var error = await Assert.ThrowsAsync<Domain.Common.DomainException>(() =>
            _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "OTRO5"), Customer, default));

        Assert.Equal("PROMOTION_ONE_PER_BOOKING", error.Code);
        Assert.Single(_redemptions.Saved);
    }

    [Fact]
    public async Task CancelRedemption_RemovesItAndTheAmountDueIsRecalculated()
    {
        // E-08: "Quitar cupón" borra el canje en el servidor, no solo en la pantalla
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);

        var result = await _service.CancelRedemptionAsync(BookingId, 1, Customer, default);

        Assert.Empty(_redemptions.Saved);
        Assert.Equal(0m, result.AppliedDiscounts);
        Assert.Equal(40_000m, result.AmountDue);
        Assert.Equal(40_000m, await new AmountDueCalculator(_redemptions).ForAsync(_bookings.Bookings[BookingId], default));
    }

    [Fact]
    public async Task CancelRedemption_WithAPaymentInReview_IsAConflict()
    {
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);
        _payments.Add(Domain.Payments.Payment.ReportWithReceipt(BookingId, 1, 36_000m, "data:image/png;base64,AAA",
            Customer.UserId, null, FixedClock.Now));
        await _uow.CommitAsync(default);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CancelRedemptionAsync(BookingId, 1, Customer, default));

        Assert.Equal(ErrorCodes.PaymentAlreadyReported, error.Code);
        Assert.Single(_redemptions.Saved);
    }

    [Fact]
    public async Task CancelRedemption_ThatDoesNotExist_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CancelRedemptionAsync(BookingId, 1, Customer, default));

        Assert.Equal(ErrorCodes.PromotionRedemptionNotFound, error.Code);
    }

    // ---- BIZ-02: lo que el cliente debe pagar, consultado al abrir la pantalla de pago ----

    [Fact]
    public async Task GetRedemption_WithoutACoupon_ReturnsTheFullTotalAndNoRedemption()
    {
        var result = await _service.GetRedemptionAsync(BookingId, Customer, default);

        Assert.Equal(BookingId, result.BookingId);
        Assert.Equal(40_000m, result.BookingTotal);
        Assert.Equal(0m, result.AppliedDiscounts);
        Assert.Equal(40_000m, result.AmountDue);
        Assert.Null(result.Redemption);
    }

    [Fact]
    public async Task GetRedemption_AfterRedeeming_ShowsTheCouponAndTheSameAmountTheServerWillCharge()
    {
        // al recargar la página el cliente sigue viendo su cupón y el total con descuento
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);

        var result = await _service.GetRedemptionAsync(BookingId, Customer, default);

        Assert.Equal(40_000m, result.BookingTotal);
        Assert.Equal(4_000m, result.AppliedDiscounts);
        Assert.Equal(36_000m, result.AmountDue);
        Assert.Equal(await new AmountDueCalculator(_redemptions).ForAsync(_bookings.Bookings[BookingId], default),
            result.AmountDue);
        var redemption = Assert.IsType<AppliedRedemptionDto>(result.Redemption);
        Assert.Equal(1, redemption.PromotionId);
        Assert.Equal("VERANO10", redemption.PromotionCode);
        Assert.Equal("Verano", redemption.PromotionName);
        Assert.Equal(4_000m, redemption.DiscountAmount);
    }

    [Fact]
    public async Task GetRedemption_WhenTheBookingTotalChanged_RecalculatesThePercentageCoupon()
    {
        // BIZ-05: el 10 % sigue siendo el 10 % del total de hoy, no los 4.000 del día del canje
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);
        _bookings.Bookings[BookingId] = _bookings.Bookings[BookingId] with { Total = 20_000m };

        var result = await _service.GetRedemptionAsync(BookingId, Customer, default);

        Assert.Equal(20_000m, result.BookingTotal);
        Assert.Equal(2_000m, result.AppliedDiscounts);
        Assert.Equal(18_000m, result.AmountDue);
        Assert.Equal(2_000m, result.Redemption!.DiscountAmount);
    }

    [Fact]
    public async Task GetRedemption_AfterRemovingTheCoupon_HasNoRedemption()
    {
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);
        await _service.CancelRedemptionAsync(BookingId, 1, Customer, default);

        var result = await _service.GetRedemptionAsync(BookingId, Customer, default);

        Assert.Null(result.Redemption);
        Assert.Equal(40_000m, result.AmountDue);
    }

    [Fact]
    public async Task GetRedemption_WhenThePromotionWasDeletedAfterwards_StillShowsItsCodeAndName()
    {
        await _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "VERANO10"), Customer, default);
        _promotions.Remove(_promotions.Promotions[0], 9);

        var result = await _service.GetRedemptionAsync(BookingId, Customer, default);

        Assert.Equal("VERANO10", result.Redemption!.PromotionCode);
        Assert.Equal(36_000m, result.AmountDue);
    }

    [Fact]
    public async Task GetRedemption_OfABookingThatIsNotTheCustomers_IsNotFound()
    {
        // booking-service no le devuelve al cliente reservas ajenas: igual que el DELETE, 404
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetRedemptionAsync(99, Customer, default));

        Assert.Equal(ErrorCodes.BookingNotFound, error.Code);
    }

    [Fact]
    public async Task Redeem_UnknownCode_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "NOEXISTE"), Customer, default));

        Assert.Equal(ErrorCodes.PromotionNotFound, error.Code);
    }
}
