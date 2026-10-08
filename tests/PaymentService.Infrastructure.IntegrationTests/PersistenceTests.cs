using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Loyalty;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;
using PaymentService.Infrastructure.Persistence;
using Xunit;

namespace PaymentService.Infrastructure.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class PersistenceTests
{
    private const string Receipt = "data:image/png;base64,AAA";
    private readonly DatabaseFixture _db;

    public PersistenceTests(DatabaseFixture db) => _db = db;

    private static T Get<T>(AsyncServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private static DateTime Now => DateTime.UtcNow;

    private async Task<short> CashAccountIdAsync()
    {
        await using var scope = _db.NewRequest();
        return (await Get<IPaymentAccountRepository>(scope).ListAsync(onlyActive: false, default)).First().Id;
    }

    private async Task<long> SaveReportedPaymentAsync(long bookingId)
    {
        await using var scope = _db.NewRequest();
        var payment = Payment.ReportWithReceipt(bookingId, await CashAccountIdAsync(), 50_000m, Receipt, 5, null, Now);
        Get<IPaymentRepository>(scope).Add(payment);
        await Get<IUnitOfWork>(scope).CommitAsync(default);
        return payment.Id;
    }

    private async Task<string> SavePromotionAsync()
    {
        var code = "IT" + DatabaseFixture.NewId();
        await using var scope = _db.NewRequest();
        var today = DateOnly.FromDateTime(Now);
        Get<IPromotionRepository>(scope).Add(Promotion.Create(new PromotionDefinition(code, "Prueba", null, 50_000m, 60,
            null, false, new[] { "Lavado", "Encerado" }, today.AddDays(-1), today.AddDays(5), DiscountType.Percentage, 10m, 0)));
        await Get<IUnitOfWork>(scope).CommitAsync(default);
        return code;
    }

    // ---- Pagos ----

    [IntegrationFact]
    public async Task Payment_IsSavedWithItsReceiptAndStatusUsesTheFixedCatalogId()
    {
        var bookingId = DatabaseFixture.NewId();
        var paymentId = await SaveReportedPaymentAsync(bookingId);

        await using var scope = _db.NewRequest();
        var payments = Get<IPaymentRepository>(scope);
        var payment = (await payments.GetByIdAsync(paymentId, default))!;
        Assert.Equal(PaymentStatus.InReview, payment.Status);
        Assert.Single(payment.Receipts);
        Assert.True(await payments.HasOpenPaymentAsync(bookingId, default));

        payment.Approve(9, Now);
        await Get<IUnitOfWork>(scope).CommitAsync(default);

        Assert.True(await payments.HasApprovedPaymentAsync(bookingId, default));
        var row = await Get<PaymentDbContext>(scope).Database
            .SqlQuery<RowState>($"SELECT payment_status_id AS StatusId, row_version AS RowVersion FROM payment.payment WHERE payment_id = {paymentId}")
            .SingleAsync();
        Assert.Equal(3, row.StatusId); // APPROVED = 3 (índice ux_payment_one_approved_per_booking)
        Assert.Equal(2, row.RowVersion); // insert = 1, aprobación = 2
    }

    [IntegrationFact]
    public async Task TwoAdminsApprovingTheSamePaymentAtOnce_TheSecondGetsAConflict()
    {
        var paymentId = await SaveReportedPaymentAsync(DatabaseFixture.NewId());

        await using var first = _db.NewRequest();
        await using var second = _db.NewRequest();
        var a = (await Get<IPaymentRepository>(first).GetByIdAsync(paymentId, default))!;
        var b = (await Get<IPaymentRepository>(second).GetByIdAsync(paymentId, default))!;

        a.Approve(9, Now);
        await Get<IUnitOfWork>(first).CommitAsync(default);
        b.Reject(10, "Llegó tarde", Now);

        await Assert.ThrowsAsync<ConflictException>(() => Get<IUnitOfWork>(second).CommitAsync(default));
    }

    [IntegrationFact]
    public async Task TransactionReference_IsDetectedOnAnotherBooking()
    {
        var reference = "REF-" + DatabaseFixture.NewId();
        var bookingId = DatabaseFixture.NewId();
        await using (var scope = _db.NewRequest())
        {
            Get<IPaymentRepository>(scope).Add(Payment.ReportWithReceipt(bookingId, await CashAccountIdAsync(), 1_000m,
                Receipt, 5, reference, Now));
            await Get<IUnitOfWork>(scope).CommitAsync(default);
        }

        await using var check = _db.NewRequest();
        var payments = Get<IPaymentRepository>(check);
        Assert.True(await payments.IsTransactionReferenceUsedElsewhereAsync(reference, DatabaseFixture.NewId(), default));
        Assert.False(await payments.IsTransactionReferenceUsedElsewhereAsync(reference, bookingId, default));
    }

    // ---- Promociones ----

    [IntegrationFact]
    public async Task Promotion_RoundTripsDiscountTypeAndBenefits_AndSoftDeleteHidesIt()
    {
        var code = await SavePromotionAsync();

        await using var scope = _db.NewRequest();
        var promotions = Get<IPromotionRepository>(scope);
        var promotion = (await promotions.GetByCodeAsync(code.ToLowerInvariant(), default))!;
        Assert.Equal(DiscountType.Percentage, promotion.DiscountType);
        Assert.Equal(new[] { "Lavado", "Encerado" }, promotion.Benefits);

        promotions.Remove(promotion, 9);
        await Get<IUnitOfWork>(scope).CommitAsync(default);

        await using var after = _db.NewRequest();
        Assert.Null(await Get<IPromotionRepository>(after).GetByCodeAsync(code, default));
    }

    [IntegrationFact]
    public async Task TwoCustomersRedeemingTheSamePromotionAtOnce_TheSecondGetsAConflict()
    {
        var code = await SavePromotionAsync();

        await using var first = _db.NewRequest();
        await using var second = _db.NewRequest();
        var a = (await Get<IPromotionRepository>(first).GetByCodeAsync(code, default))!;
        var b = (await Get<IPromotionRepository>(second).GetByCodeAsync(code, default))!;

        Get<IPromotionRedemptionRepository>(first).Add(a.Redeem(Request(DatabaseFixture.NewId())));
        await Get<IUnitOfWork>(first).CommitAsync(default);
        Get<IPromotionRedemptionRepository>(second).Add(b.Redeem(Request(DatabaseFixture.NewId())));

        // El segundo leyó la promoción antes del primer canje: su conteo de usos ya no es válido.
        await Assert.ThrowsAsync<ConflictException>(() => Get<IUnitOfWork>(second).CommitAsync(default));

        await using var check = _db.NewRequest();
        Assert.Equal(1, await Get<IPromotionRedemptionRepository>(check).CountAsync(a.Id, default));
    }

    private static RedemptionRequest Request(long bookingId) =>
        new(bookingId, "RES", DatabaseFixture.NewId(), Now, 0, 40_000m, false, 0, 0);

    // ---- Ledger de puntos ----

    [IntegrationFact]
    public async Task Ledger_EarnAndReverse_UpdatesBalanceAndNetPoints()
    {
        var customer = DatabaseFixture.NewId();
        var booking = DatabaseFixture.NewId();

        await using (var scope = _db.NewRequest())
        {
            var ledger = Get<ILoyaltyLedgerRepository>(scope);
            ledger.Add(LoyaltyTransaction.Earn(customer, booking, 25, await ledger.CurrentBalanceAsync(customer, default), 9, Now));
            await Get<IUnitOfWork>(scope).CommitAsync(default);

            Assert.Equal(new LoyaltyBalance(25, 1), await ledger.CurrentBalanceAsync(customer, default));
            Assert.Equal(25, (await ledger.NetPointsByCustomerForBookingAsync(booking, default))[customer]);
        }

        await using (var scope = _db.NewRequest())
        {
            var ledger = Get<ILoyaltyLedgerRepository>(scope);
            ledger.Add(LoyaltyTransaction.ReverseEarned(customer, booking, 25, await ledger.CurrentBalanceAsync(customer, default), 9, Now)!);
            await Get<IUnitOfWork>(scope).CommitAsync(default);

            Assert.Equal(new LoyaltyBalance(0, 2), await ledger.CurrentBalanceAsync(customer, default));
            Assert.Empty(await ledger.NetPointsByCustomerForBookingAsync(booking, default));
        }
    }

    [IntegrationFact]
    public async Task TwoMovementsComputedOnTheSameBalance_TheSecondGetsAConflict()
    {
        var customer = DatabaseFixture.NewId();

        await using var first = _db.NewRequest();
        await using var second = _db.NewRequest();
        var ledgerA = Get<ILoyaltyLedgerRepository>(first);
        var ledgerB = Get<ILoyaltyLedgerRepository>(second);
        var balanceA = await ledgerA.CurrentBalanceAsync(customer, default);
        var balanceB = await ledgerB.CurrentBalanceAsync(customer, default);

        ledgerA.Add(LoyaltyTransaction.Earn(customer, DatabaseFixture.NewId(), 10, balanceA, 9, Now));
        await Get<IUnitOfWork>(first).CommitAsync(default);
        ledgerB.Add(LoyaltyTransaction.Earn(customer, DatabaseFixture.NewId(), 20, balanceB, 9, Now));

        await Assert.ThrowsAsync<ConflictException>(() => Get<IUnitOfWork>(second).CommitAsync(default));

        await using var check = _db.NewRequest();
        Assert.Equal(10, (await Get<ILoyaltyLedgerRepository>(check).CurrentBalanceAsync(customer, default)).Points);
    }

    private sealed record RowState(short StatusId, int RowVersion);
}
