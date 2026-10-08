using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.UnitTests.Fakes;
using PaymentService.Domain.Promotions;
using Xunit;

namespace PaymentService.Application.UnitTests.Payments;

public class PaymentQueryServiceTests
{
    private const long BookingId = 7;

    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeRedemptionRepository _redemptions;
    private readonly FakeBookingDirectory _bookings = new();
    private readonly PaymentQueryService _service;

    public PaymentQueryServiceTests()
    {
        _redemptions = new FakeRedemptionRepository(_uow);
        _service = new PaymentQueryService(new FakePaymentRepository(_uow), _bookings,
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
}
