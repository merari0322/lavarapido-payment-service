using PaymentService.Domain.Common;
using PaymentService.Domain.Loyalty;
using Xunit;

namespace PaymentService.Domain.UnitTests.Loyalty;

public class LoyaltyTransactionTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);

    // ---- Ganar puntos ----

    [Fact]
    public void Earn_AddsPointsToTheCurrentBalanceAndTakesTheNextPosition()
    {
        var transaction = LoyaltyTransaction.Earn(customerId: 3, bookingId: 7, points: 25,
            current: new LoyaltyBalance(100, 4), actor: 9, nowUtc: Now);

        Assert.Equal(LoyaltyMovementType.Earned, transaction.MovementType);
        Assert.Equal(25, transaction.Points);
        Assert.Equal(125, transaction.BalanceAfter);
        Assert.Equal(5, transaction.Sequence);
        Assert.Equal(7, transaction.BookingId);
        Assert.Equal(9, transaction.CreatedBy);
        Assert.Equal(Now, transaction.CreatedAtUtc);
        Assert.Equal(new LoyaltyBalance(125, 5), transaction.ResultingBalance);
    }

    [Fact]
    public void Earn_FirstMovementOfTheCustomer_StartsAtPositionOne()
    {
        var transaction = LoyaltyTransaction.Earn(3, 7, 10, LoyaltyBalance.Empty, null, Now);

        Assert.Equal(1, transaction.Sequence);
        Assert.Equal(10, transaction.BalanceAfter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Earn_WithZeroOrNegativePoints_ThrowsDomainException(int points)
    {
        Assert.Throws<DomainException>(() => LoyaltyTransaction.Earn(3, 7, points, LoyaltyBalance.Empty, null, Now));
    }

    [Fact]
    public void Earn_WithoutBooking_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => LoyaltyTransaction.Earn(3, 0, 10, LoyaltyBalance.Empty, null, Now));
    }

    // ---- Revertir puntos (reembolso) ----

    [Fact]
    public void ReverseEarned_SubtractsTheEarnedPoints()
    {
        var reversal = LoyaltyTransaction.ReverseEarned(3, 7, earnedPoints: 25, new LoyaltyBalance(125, 5), 9, Now)!;

        Assert.Equal(LoyaltyMovementType.Reversed, reversal.MovementType);
        Assert.Equal(-25, reversal.Points);
        Assert.Equal(100, reversal.BalanceAfter);
        Assert.Equal(6, reversal.Sequence);
        Assert.Equal(7, reversal.BookingId);
    }

    [Fact]
    public void ReverseEarned_WhenSomePointsAreGone_OnlyReversesWhatIsLeft()
    {
        var reversal = LoyaltyTransaction.ReverseEarned(3, 7, earnedPoints: 25, new LoyaltyBalance(10, 8), 9, Now)!;

        Assert.Equal(-10, reversal.Points);
        Assert.Equal(0, reversal.BalanceAfter);
    }

    [Fact]
    public void ReverseEarned_WithNoBalance_ReturnsNothing()
    {
        Assert.Null(LoyaltyTransaction.ReverseEarned(3, 7, earnedPoints: 25, LoyaltyBalance.Empty, 9, Now));
    }

    // ---- Saldo ----

    [Fact]
    public void LoyaltyBalance_CannotBeNegative()
    {
        Assert.Throws<DomainException>(() => new LoyaltyBalance(-1, 0));
    }

    [Theory]
    [InlineData(LoyaltyMovementType.Earned, 1)]
    [InlineData(LoyaltyMovementType.Redeemed, -1)]
    [InlineData(LoyaltyMovementType.Expired, -1)]
    [InlineData(LoyaltyMovementType.Adjusted, 1)]
    [InlineData(LoyaltyMovementType.Reversed, -1)]
    public void Sign_MatchesTheCatalog(LoyaltyMovementType type, int expected)
    {
        Assert.Equal(expected, type.Sign());
    }
}
