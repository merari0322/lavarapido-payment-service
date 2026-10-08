using PaymentService.Domain.Payments;
using Xunit;

namespace PaymentService.Domain.UnitTests.Payments;

public class PaymentPolicyTests
{
    [Theory]
    [InlineData("CONFIRMED", true)]
    [InlineData("in_progress", true)]
    [InlineData("COMPLETED", true)]
    [InlineData("CANCELLED", false)]
    [InlineData("NO_SHOW", false)]
    [InlineData("PENDING", false)]
    public void IsBookingPayable_OnlyForActiveOrFinishedBookings(string status, bool expected)
    {
        Assert.Equal(expected, PaymentPolicy.IsBookingPayable(status));
    }

    [Theory]
    [InlineData(50_000, 0, 50_000)]
    [InlineData(50_000, 10_000, 40_000)]
    [InlineData(50_000, 60_000, 0)]
    public void AmountDue_SubtractsDiscountsAndNeverGoesNegative(decimal total, decimal discounts, decimal expected)
    {
        Assert.Equal(expected, PaymentPolicy.AmountDue(total, discounts));
    }
}
