using System.Text.Json;
using PaymentService.Api.Contracts;
using Xunit;

namespace PaymentService.Api.UnitTests.Contracts;

// BIZ-13: sin la reserva o la cuenta el cuerpo no se acepta (antes se leían como 0).
public class RequiredIdsTests
{
    // las mismas opciones con las que ASP.NET Core lee el cuerpo JSON (camelCase, sin distinguir mayúsculas)
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("""{"paymentAccountId":1,"receiptImage":"data:image/png;base64,AAA"}""")]
    [InlineData("""{"bookingId":7,"receiptImage":"data:image/png;base64,AAA"}""")]
    public void ReportPayment_WithoutTheBookingOrTheAccount_IsRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReportPaymentRequest>(json, Web));
    }

    [Fact]
    public void ReportPayment_WithBoth_IsRead()
    {
        var request = JsonSerializer.Deserialize<ReportPaymentRequest>(
            """{"bookingId":7,"paymentAccountId":2,"receiptImage":"data:image/png;base64,AAA"}""", Web)!;

        Assert.Equal(7, request.BookingId);
        Assert.Equal(2, request.PaymentAccountId);
        Assert.Null(request.ReportedAmount);
    }

    [Theory]
    [InlineData("""{"paymentAccountId":1}""")]
    [InlineData("""{"bookingId":7}""")]
    public void ManualPayment_WithoutTheBookingOrTheAccount_IsRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ManualPaymentRequest>(json, Web));
    }

    [Fact]
    public void RedeemPromotion_WithoutTheBooking_IsRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RedeemPromotionRequest>("""{"code":"VERANO10"}""", Web));
        Assert.Equal(7, JsonSerializer.Deserialize<RedeemPromotionRequest>("""{"bookingId":7,"code":"VERANO10"}""", Web)!.BookingId);
    }
}
