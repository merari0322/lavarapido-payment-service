using PaymentService.Application.Common;
using PaymentService.Infrastructure.Persistence;
using Xunit;

namespace PaymentService.Infrastructure.IntegrationTests;

// A-16: cada índice único que protege una regla responde 409 con el código de esa regla. No necesita
// base: se prueba con el texto que manda SQL Server (2601/2627).
public sealed class UniqueIndexConflictsTests
{
    private static string Duplicate(string table, string index) =>
        $"Cannot insert duplicate key row in object '{table}' with unique index '{index}'. The duplicate key value is (42).";

    [Theory]
    [InlineData("payment.payment", "ux_payment_one_open_per_booking", ErrorCodes.PaymentAlreadyReported)]
    [InlineData("payment.v_live_transaction_reference", "ux_live_transaction_reference", ErrorCodes.TransactionReferenceReused)]
    [InlineData("payment.payment", "ux_payment_one_approved_per_booking", ErrorCodes.PaymentAlreadyApproved)]
    [InlineData("promotion.booking_promotion", "ux_booking_promotion_one_per_booking", "PROMOTION_ONE_PER_BOOKING")]
    public void For_AKnownIndex_IsTheCodeOfItsRule(string table, string index, string expectedCode)
    {
        var conflict = UniqueIndexConflicts.For(Duplicate(table, index));

        Assert.NotNull(conflict);
        Assert.Equal(expectedCode, conflict.Code);
    }

    [Fact]
    public void For_AnotherIndex_IsNullSoItStaysADataConflict()
    {
        Assert.Null(UniqueIndexConflicts.For(Duplicate("payment.loyalty_transaction", "ux_loyalty_transaction_customer_sequence")));
    }
}
