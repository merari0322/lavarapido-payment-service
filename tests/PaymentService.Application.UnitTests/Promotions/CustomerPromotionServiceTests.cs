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
    private readonly FakeBookingDirectory _bookings = new();
    private readonly RecordingPublisher _events = new();
    private readonly CustomerPromotionService _service;

    public CustomerPromotionServiceTests()
    {
        _redemptions = new FakeRedemptionRepository(_uow);
        var payments = new FakePaymentRepository(_uow);
        _service = new CustomerPromotionService(_promotions, _redemptions, new FakeLedger(_uow), _bookings,
            new BookingPaymentGuard(payments, new FakePaymentAccountRepository()), new AmountDueCalculator(_redemptions),
            _uow, _events, new FixedClock());

        _bookings.Bookings[BookingId] = new BookingInfo(BookingId, "RES-7", "CONFIRMED", 40_000m, "2026-10-08", "10:00",
            "Lavado", "Mazda 3", "ABC123", Customer.UserId, 0);

        var today = DateOnly.FromDateTime(FixedClock.Now);
        _promotions.Add(Promotion.Create(new PromotionDefinition("VERANO10", "Verano", null, 50_000m, 60, null, false,
                Array.Empty<string>(), today.AddDays(-1), today.AddDays(30), DiscountType.Percentage, 10m, 0))
            .WithId<Promotion, int>(1));
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
    public async Task Redeem_UnknownCode_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RedeemAsync(new RedeemPromotionCommand(BookingId, "NOEXISTE"), Customer, default));

        Assert.Equal(ErrorCodes.PromotionNotFound, error.Code);
    }
}
