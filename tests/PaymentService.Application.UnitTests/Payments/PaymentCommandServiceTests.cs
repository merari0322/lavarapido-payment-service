using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Loyalty;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.UnitTests.Fakes;
using PaymentService.Domain.Loyalty;
using PaymentService.Domain.PaymentAccounts;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;
using Xunit;

namespace PaymentService.Application.UnitTests.Payments;

public class PaymentCommandServiceTests
{
    private const long CustomerId = 3;
    private const long BookingId = 7;
    private const int BookingPoints = 25;
    private static readonly Caller Admin = new(9);
    private static readonly Caller Customer = new(CustomerId);

    private readonly FakeUnitOfWork _uow = new();
    private readonly FakePaymentRepository _payments;
    private readonly FakePaymentAccountRepository _accounts = new();
    private readonly FakeLedger _ledger;
    private readonly FakeRedemptionRepository _redemptions;
    private readonly FakeBookingDirectory _bookings = new();
    private readonly FakePromotionRepository _promotions = new();
    private readonly RecordingPublisher _events = new();
    private readonly PaymentCommandService _service;

    public PaymentCommandServiceTests()
    {
        _payments = new FakePaymentRepository(_uow);
        _ledger = new FakeLedger(_uow);
        _redemptions = new FakeRedemptionRepository(_uow);
        _service = new PaymentCommandService(_payments, _bookings, new BookingPaymentGuard(_payments, _accounts),
            new AmountDueCalculator(_redemptions), new LoyaltyRewardService(_ledger, _promotions), _uow, _events,
            new PaymentDtoAssembler(new PaymentAccountReader(_accounts)), new FixedClock());

        _bookings.Bookings[BookingId] = new BookingInfo(BookingId, "RES-7", "CONFIRMED", 50_000m, "2026-10-08", "10:00",
            "Lavado", "Mazda 3", "ABC123", CustomerId, BookingPoints);

        var nequi = Ids.Create<PaymentMethodType>(("Code", "NEQUI"), ("Name", "Nequi"), ("RequiresReceipt", true), ("IsActive", true))
            .WithId<PaymentMethodType, short>(2);
        _accounts.Methods.Add(nequi);
        _accounts.Accounts.Add(PaymentAccount.Create(nequi, "Lavadero", "300", null, null, active: true).WithId<PaymentAccount, short>(1));
        _accounts.Accounts.Add(PaymentAccount.Create(nequi, "Vieja", "301", null, null, active: false).WithId<PaymentAccount, short>(2));
    }

    private async Task<long> ReportedPaymentAsync()
    {
        var dto = await _service.ReportAsync(new ReportPaymentCommand(BookingId, 1, "REF-1", "data:image/png;base64,AAA"),
            Customer, default);
        return dto.Id;
    }

    // ---- Reportar ----

    [Fact]
    public async Task Report_ChargesWhatIsLeftToPayAndMapsTheBookingToItsOwnContract()
    {
        var dto = await _service.ReportAsync(new ReportPaymentCommand(BookingId, 1, null, "data:image/png;base64,AAA"),
            Customer, default);

        Assert.Equal("IN_REVIEW", dto.Status);
        Assert.Equal(50_000m, dto.Amount);
        Assert.Equal("RES-7", dto.Booking!.Code);
        Assert.Equal(CustomerId, dto.Booking.OwnerUserId);
        Assert.Equal(FixedClock.Now, dto.ReportedAtUtc);
    }

    [Fact]
    public async Task Report_ToAnInactiveAccount_IsAnInvalidRequest()
    {
        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _service.ReportAsync(new ReportPaymentCommand(BookingId, 2, null, "data:image/png;base64,AAA"), Customer, default));

        Assert.Equal(ErrorCodes.InvalidPaymentAccount, error.Code);
        Assert.Empty(_payments.Saved);
    }

    // ---- Aprobar ----

    [Fact]
    public async Task Approve_CreditsTheBookingPointsAndPublishesTheConfirmation()
    {
        var paymentId = await ReportedPaymentAsync();

        var dto = await _service.ApproveAsync(paymentId, Admin, default);

        Assert.Equal("APPROVED", dto.Status);
        Assert.Equal(BookingPoints, _ledger.BalanceOf(CustomerId));
        // primero el pago, después los puntos que ganó
        Assert.Equal(new[] { "payment.confirmed", "payment.loyalty_points_earned" },
            _events.Published.Select(e => e.RoutingKey));
        Assert.All(_events.Published, e => Assert.Equal(FixedClock.Now, e.OccurredOnUtc));
        Assert.Empty((await _payments.GetByIdAsync(paymentId, default))!.DomainEvents);
    }

    [Fact]
    public async Task Approve_TellsHowManyPointsWereEarnedAndWhichCouponsTheyUnlocked()
    {
        var today = DateOnly.FromDateTime(FixedClock.Now);
        Promotion Coupon(string code, int points) => Promotion.Create(new PromotionDefinition(code, "Cupón " + code, null,
            null, null, null, false, Array.Empty<string>(), today.AddDays(-1), today.AddDays(30),
            DiscountType.Percentage, 10m, points));
        _promotions.Add(Coupon("GRATIS0", 0));      // no pide puntos: no se "desbloquea"
        _promotions.Add(Coupon("LAVA20", 20));      // 0 -> 25 la desbloquea
        _promotions.Add(Coupon("VIP100", 100));     // todavía no
        var paymentId = await ReportedPaymentAsync();

        await _service.ApproveAsync(paymentId, Admin, default);

        var earned = Assert.Single(_events.Published, e => e.RoutingKey == "payment.loyalty_points_earned");
        Assert.Equal("LoyaltyPointsEarned", earned.EventType);
        Assert.Equal(CustomerId, earned.Payload["customerUserId"]);
        Assert.Equal("RES-7", earned.Payload["bookingCode"]);
        Assert.Equal(BookingPoints, earned.Payload["points"]);
        Assert.Equal(BookingPoints, earned.Payload["balance"]);
        var unlocked = Assert.Single((IEnumerable<Dictionary<string, object?>>)earned.Payload["unlockedPromotions"]!);
        Assert.Equal("LAVA20", unlocked["code"]);
        Assert.Equal(10, unlocked["discountPercent"]);
    }

    [Fact]
    public async Task Approve_WhenTheBookingEarnsNoPoints_PublishesOnlyTheConfirmation()
    {
        _bookings.Bookings[BookingId] = _bookings.Bookings[BookingId] with { TotalLoyaltyPoints = 0 };
        var paymentId = await ReportedPaymentAsync();

        await _service.ApproveAsync(paymentId, Admin, default);

        Assert.Equal("payment.confirmed", Assert.Single(_events.Published).RoutingKey);
    }

    [Fact]
    public async Task RegisterInPerson_AlsoPublishesThePointsEarned()
    {
        await _service.RegisterInPersonAsync(new RegisterInPersonPaymentCommand(BookingId, 1, null), Admin, default);

        Assert.Contains(_events.Published, e => e.RoutingKey == "payment.loyalty_points_earned");
    }

    [Fact]
    public async Task Approve_WhenTheBookingNoLongerExists_FailsWithoutApproving()
    {
        var paymentId = await ReportedPaymentAsync();
        _bookings.Bookings.Clear();
        var commitsBefore = _uow.Commits;

        var error = await Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveAsync(paymentId, Admin, default));

        Assert.Equal(ErrorCodes.BookingNotFound, error.Code);
        Assert.Equal(PaymentStatus.InReview, (await _payments.GetByIdAsync(paymentId, default))!.Status);
        Assert.Equal(commitsBefore, _uow.Commits);
        Assert.Empty(_ledger.Saved);
    }

    [Fact]
    public async Task Approve_WhenTheBookingWasCancelledWhileInReview_IsAConflictAndCreditsNoPoints()
    {
        var paymentId = await ReportedPaymentAsync();
        _bookings.Bookings[BookingId] = _bookings.Bookings[BookingId] with { Status = "CANCELLED" };
        var commitsBefore = _uow.Commits;

        var error = await Assert.ThrowsAsync<ConflictException>(() => _service.ApproveAsync(paymentId, Admin, default));

        Assert.Equal(ErrorCodes.BookingNotPayable, error.Code);
        Assert.Equal(PaymentStatus.InReview, (await _payments.GetByIdAsync(paymentId, default))!.Status);
        Assert.Equal(commitsBefore, _uow.Commits);
        Assert.Empty(_ledger.Saved);
    }

    [Fact]
    public async Task Report_ShowsTheAdminWhatTheCustomerSaysTheyPaid()
    {
        var dto = await _service.ReportAsync(
            new ReportPaymentCommand(BookingId, 1, null, "data:image/png;base64,AAA", ReportedAmount: 45_000m),
            Customer, default);

        Assert.Equal(50_000m, dto.Amount);
        Assert.Equal(45_000m, dto.ReportedAmount);
    }

    [Fact]
    public async Task Approve_WhenTheBookingIsAlreadyPaid_IsAConflictAndLeavesThePaymentUntouched()
    {
        // El cliente reportó por QR y mientras tanto el admin registró el pago en persona.
        var reported = await ReportedPaymentAsync();
        await _service.RegisterInPersonAsync(new RegisterInPersonPaymentCommand(BookingId, 1, null), Admin, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => _service.ApproveAsync(reported, Admin, default));

        Assert.Equal(ErrorCodes.PaymentAlreadyApproved, error.Code);
        Assert.Equal(PaymentStatus.InReview, (await _payments.GetByIdAsync(reported, default))!.Status);
        Assert.Equal(BookingPoints, _ledger.BalanceOf(CustomerId)); // los puntos se acreditaron una sola vez
    }

    // ---- Reembolsar ----

    [Fact]
    public async Task Refund_ReversesThePointsTheBookingEarned()
    {
        var paymentId = await ReportedPaymentAsync();
        await _service.ApproveAsync(paymentId, Admin, default);

        await _service.RefundAsync(paymentId, Admin, default);

        Assert.Equal(0, _ledger.BalanceOf(CustomerId));
        var reversal = _ledger.Saved.Last();
        Assert.Equal(LoyaltyMovementType.Reversed, reversal.MovementType);
        Assert.Equal(-BookingPoints, reversal.Points);
        Assert.Equal(2, reversal.Sequence);
        Assert.Contains(_events.Published, e => e.RoutingKey == "payment.refunded");
    }

    [Fact]
    public async Task Refund_ThenPayingAgain_CreditsThePointsAgain()
    {
        var first = await ReportedPaymentAsync();
        await _service.ApproveAsync(first, Admin, default);
        await _service.RefundAsync(first, Admin, default);

        await _service.RegisterInPersonAsync(new RegisterInPersonPaymentCommand(BookingId, 1, null), Admin, default);

        Assert.Equal(BookingPoints, _ledger.BalanceOf(CustomerId));
        Assert.Equal(3, _ledger.Saved.Count);
    }
}
