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

    [Fact]
    public void CanReportPayment_WithoutPayments_DependsOnTheBookingStatus()
    {
        Assert.True(PaymentPolicy.CanReportPayment("CONFIRMED", []));
        Assert.True(PaymentPolicy.CanReportPayment("COMPLETED", []));
        Assert.False(PaymentPolicy.CanReportPayment("CANCELLED", []));
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.InReview, false)]
    [InlineData(PaymentStatus.Approved, false)]
    [InlineData(PaymentStatus.Rejected, true)]
    [InlineData(PaymentStatus.Refunded, true)]
    public void CanReportPayment_IsBlockedByAnApprovedOrOpenPayment(PaymentStatus existing, bool expected)
    {
        Assert.Equal(expected, PaymentPolicy.CanReportPayment("CONFIRMED", [existing]));
    }

    [Fact]
    public void CanReportPayment_LooksAtEveryPaymentOfTheBooking()
    {
        // un pago rechazado más reciente no reabre la reserva si otro ya quedó aprobado
        Assert.False(PaymentPolicy.CanReportPayment("CONFIRMED", [PaymentStatus.Rejected, PaymentStatus.Approved]));
    }

    [Theory]
    [InlineData(50_000, 0, 50_000)]
    [InlineData(50_000, 10_000, 40_000)]
    [InlineData(50_000, 60_000, 0)]
    public void AmountDue_SubtractsDiscountsAndNeverGoesNegative(decimal total, decimal discounts, decimal expected)
    {
        Assert.Equal(expected, PaymentPolicy.AmountDue(total, discounts));
    }

    [Theory]
    [InlineData("50000", "50000", true)]
    [InlineData("36000.00", "36000", true)]
    [InlineData("33333.333", "33333.33", true)]  // el pago se guarda en centavos
    [InlineData("50000", "60000", false)]        // BIZ-01: la reserva se reprogramó con otros servicios
    [InlineData("50000", "49999.99", false)]
    public void AmountStillMatches_ComparesInCents(string paymentAmount, string amountDue, bool expected)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(expected, PaymentPolicy.AmountStillMatches(decimal.Parse(paymentAmount, culture),
            decimal.Parse(amountDue, culture)));
    }
}
