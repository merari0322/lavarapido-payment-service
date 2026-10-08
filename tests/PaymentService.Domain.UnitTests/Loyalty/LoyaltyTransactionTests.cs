using PaymentService.Domain.Common;
using PaymentService.Domain.Loyalty;
using Xunit;

namespace PaymentService.Domain.UnitTests.Loyalty;

public class LoyaltyTransactionTests
{
    [Fact]
    public void Earn_AddsPointsToTheCurrentBalance()
    {
        var transaction = LoyaltyTransaction.Earn(customerId: 3, bookingId: 7, points: 25, currentBalance: 100, actor: 9);

        Assert.Equal(LoyaltyMovementType.Earned, transaction.MovementType);
        Assert.Equal(25, transaction.Points);
        Assert.Equal(125, transaction.BalanceAfter);
        Assert.Equal(7, transaction.BookingId);
        Assert.Equal(9, transaction.CreatedBy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Earn_WithZeroOrNegativePoints_ThrowsDomainException(int points)
    {
        Assert.Throws<DomainException>(() => LoyaltyTransaction.Earn(3, 7, points, 0, null));
    }

    [Fact]
    public void Earn_WithoutBooking_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => LoyaltyTransaction.Earn(3, 0, 10, 0, null));
    }

    [Theory]
    [InlineData(LoyaltyMovementType.Earned, 1)]
    [InlineData(LoyaltyMovementType.Redeemed, -1)]
    [InlineData(LoyaltyMovementType.Expired, -1)]
    [InlineData(LoyaltyMovementType.Adjusted, 1)]
    public void Sign_MatchesTheSeededCatalog(LoyaltyMovementType type, int expected)
    {
        Assert.Equal(expected, type.Sign());
    }
}
