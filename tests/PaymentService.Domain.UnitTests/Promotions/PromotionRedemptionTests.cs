using System.Reflection;
using PaymentService.Domain.Promotions;
using Xunit;

namespace PaymentService.Domain.UnitTests.Promotions;

// BIZ-05: lo que descuenta hoy un canje cuando cambia el total de la reserva.
public class PromotionRedemptionTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static Promotion Coupon(DiscountType type, decimal value) =>
        Promotion.Create(new PromotionDefinition("CUPON", "Cupón", null, null, null, null, false, Array.Empty<string>(),
            Today.AddDays(-1), Today.AddDays(30), type, value, 0));

    private static PromotionRedemption Redeem(Promotion promotion, decimal subtotal) =>
        promotion.Redeem(new RedemptionRequest(7, "RES-7", 3, Now, 0, subtotal, false, 0, 0));

    // Los valores que solo llegan desde la base (canjes de antes, topes) se fijan por reflexión.
    private static void Set(object target, string property, object? value) =>
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(target, value);

    [Fact]
    public void Redeem_APercentageCoupon_KeepsItsPercentage()
    {
        var redemption = Redeem(Coupon(DiscountType.Percentage, 10m), 40_000m);

        Assert.Equal(4_000m, redemption.AppliedAmount);
        Assert.Equal(10m, redemption.DiscountPercent);
        Assert.Null(redemption.MaxDiscountAmount);
    }

    [Fact]
    public void DiscountOn_APercentageCoupon_FollowsTheNewTotal()
    {
        // 50 % sobre 40.000 y la reserva se reprograma a 20.000: antes quedaba 0 por pagar
        var redemption = Redeem(Coupon(DiscountType.Percentage, 50m), 40_000m);

        Assert.Equal(10_000m, redemption.DiscountOn(20_000m));
        Assert.Equal(30_000m, redemption.DiscountOn(60_000m));
    }

    [Fact]
    public void DiscountOn_KeepsTheCapThePromotionHadWhenRedeemed()
    {
        var promotion = Coupon(DiscountType.Percentage, 50m);
        Set(promotion, nameof(Promotion.MaxDiscountAmount), 5_000m);
        var redemption = Redeem(promotion, 40_000m);

        Assert.Equal(5_000m, redemption.MaxDiscountAmount);
        Assert.Equal(5_000m, redemption.DiscountOn(100_000m));
        Assert.Equal(3_000m, redemption.DiscountOn(6_000m));
    }

    [Fact]
    public void DiscountOn_AFixedCoupon_StaysTheSameAmountButNeverLeavesZero()
    {
        var redemption = Redeem(Coupon(DiscountType.FixedAmount, 5_000m), 40_000m);

        Assert.Null(redemption.DiscountPercent);
        Assert.Equal(5_000m, redemption.DiscountOn(60_000m));
        Assert.Equal(2_999m, redemption.DiscountOn(3_000m));
    }

    [Fact]
    public void DiscountOn_ARedemptionSavedBeforeThePercentage_UsesTheFrozenAmountButNeverLeavesZero()
    {
        var redemption = Redeem(Coupon(DiscountType.Percentage, 50m), 40_000m);
        Set(redemption, nameof(PromotionRedemption.DiscountPercent), null);

        Assert.Equal(20_000m, redemption.DiscountOn(40_000m));
        Assert.Equal(19_999m, redemption.DiscountOn(20_000m));
        Assert.Equal(0m, redemption.DiscountOn(1m));
    }

    [Fact]
    public void DiscountsOn_EachRedemptionIsCalculatedOnWhatThePreviousLeft()
    {
        // datos de antes de E-09: dos cupones en la misma reserva
        var first = Redeem(Coupon(DiscountType.Percentage, 10m), 50_000m);
        var second = Redeem(Coupon(DiscountType.Percentage, 50m), 45_000m);

        var discounts = PromotionRedemption.DiscountsOn(30_000m, new[] { first, second });

        Assert.Equal(new[] { 3_000m, 13_500m }, discounts.Select(d => d.Amount));
        Assert.Same(second, discounts[1].Redemption);
    }
}
