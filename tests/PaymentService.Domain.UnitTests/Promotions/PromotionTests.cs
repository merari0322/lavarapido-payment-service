using System.Reflection;
using PaymentService.Domain.Common;
using PaymentService.Domain.Promotions;
using Xunit;

namespace PaymentService.Domain.UnitTests.Promotions;

public class PromotionTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static PromotionDefinition Definition(DiscountType type = DiscountType.Percentage, decimal value = 10m,
        int requiredPoints = 0, decimal? price = 50_000m, int? duration = 60) =>
        new("verano10", "Verano", null, price, duration, null, false, new[] { " Lavado ", "", "Encerado" },
            Today.AddDays(-1), Today.AddDays(30), type, value, requiredPoints);

    private static RedemptionRequest Request(decimal subtotal = 40_000m, int points = 0, bool alreadyRedeemed = false,
        int total = 0, int byCustomer = 0) =>
        new(BookingId: 7, BookingCode: "RES-7", CustomerUserId: 3, Now, points, subtotal, alreadyRedeemed, total, byCustomer);

    // Los límites solo se cargan desde la base; en la prueba se fijan por reflexión.
    private static void SetLimit(Promotion promotion, string property, object? value) =>
        typeof(Promotion).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(promotion, value);

    // ---- Crear / editar ----

    [Fact]
    public void Create_NormalizesCodeAndBenefitsAndStartsActive()
    {
        var promotion = Promotion.Create(Definition());

        Assert.Equal("VERANO10", promotion.Code);
        Assert.Equal(new[] { "Lavado", "Encerado" }, promotion.Benefits);
        Assert.True(promotion.IsActive);
        Assert.Equal(10, promotion.DiscountPercent);
    }

    [Fact]
    public void Create_WithoutReferencePriceOrDuration_IsAValidCoupon()
    {
        var promotion = Promotion.Create(Definition(price: null, duration: null));

        Assert.Null(promotion.Price);
        Assert.Null(promotion.DurationMinutes);
        Assert.Equal(4_000m, promotion.CalculateDiscount(40_000m));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(50_000, 0)]
    public void Create_WithAReferencePriceOrDurationThatIsNotPositive_ThrowsDomainException(int price, int duration)
    {
        Assert.Throws<DomainException>(() => Promotion.Create(Definition(price: price, duration: duration)));
    }

    // ---- Desbloqueo por puntos ----

    [Theory]
    [InlineData(20, 30, true)]   // cruza los 30 que pide
    [InlineData(20, 45, true)]   // los pasa de largo
    [InlineData(30, 40, false)]  // ya la tenía desbloqueada
    [InlineData(10, 25, false)]  // todavía no llega
    public void IsUnlockedBy_IsTrueOnlyWhenTheBalanceCrossesTheRequiredPoints(int before, int after, bool expected)
    {
        var promotion = Promotion.Create(Definition(requiredPoints: 30));

        Assert.Equal(expected, promotion.IsUnlockedBy(before, after, Today));
    }

    [Fact]
    public void IsUnlockedBy_IgnoresPausedOrNotYetValidPromotions()
    {
        var paused = Promotion.Create(Definition(requiredPoints: 30));
        paused.SetActive(false);

        Assert.False(paused.IsUnlockedBy(20, 30, Today));
        Assert.False(Promotion.Create(Definition(requiredPoints: 30)).IsUnlockedBy(20, 30, Today.AddDays(-5)));
    }

    [Fact]
    public void IsUnlockedBy_APromotionWithoutRequiredPointsIsNeverNewlyUnlocked()
    {
        Assert.False(Promotion.Create(Definition(requiredPoints: 0)).IsUnlockedBy(0, 10, Today));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Create_WithPercentOutOfRange_ThrowsDomainException(int percent)
    {
        Assert.Throws<DomainException>(() => Promotion.Create(Definition(value: percent)));
    }

    [Fact]
    public void Create_WithPackageType_ThrowsDomainException()
    {
        var error = Assert.Throws<DomainException>(() => Promotion.Create(Definition(DiscountType.Package, 45_000m)));

        Assert.Equal("PROMOTION_DISCOUNT_UNSUPPORTED", error.Code);
    }

    [Fact]
    public void Create_WithEndBeforeStart_ThrowsDomainException()
    {
        var definition = Definition() with { ValidTo = Today.AddDays(-5) };

        Assert.Throws<DomainException>(() => Promotion.Create(definition));
    }

    [Fact]
    public void StatusFor_ReflectsActiveScheduledAndPaused()
    {
        var promotion = Promotion.Create(Definition() with { ValidFrom = Today.AddDays(2) });
        Assert.Equal("scheduled", promotion.StatusFor(Today));

        promotion.SetActive(false);
        Assert.Equal("paused", promotion.StatusFor(Today));
    }

    // ---- Estrategias de descuento ----

    [Fact]
    public void CalculateDiscount_Percentage_AppliesPercentAndRounds()
    {
        var promotion = Promotion.Create(Definition(DiscountType.Percentage, 15m));

        Assert.Equal(1_500.15m, promotion.CalculateDiscount(10_001m));
    }

    [Fact]
    public void CalculateDiscount_FixedAmount_NeverExceedsSubtotal()
    {
        var promotion = Promotion.Create(Definition(DiscountType.FixedAmount, 20_000m));

        Assert.Equal(20_000m, promotion.CalculateDiscount(50_000m));
        Assert.Equal(12_000m, promotion.CalculateDiscount(12_000m));
    }

    [Fact]
    public void CalculateDiscount_RespectsMaxDiscountAmount()
    {
        var promotion = Promotion.Create(Definition(DiscountType.Percentage, 50m));
        SetLimit(promotion, nameof(Promotion.MaxDiscountAmount), 5_000m);

        Assert.Equal(5_000m, promotion.CalculateDiscount(40_000m));
    }

    // ---- Canje ----

    [Fact]
    public void Redeem_WhenAllRulesPass_FreezesTheAppliedAmount()
    {
        var promotion = Promotion.Create(Definition(value: 10m));

        var redemption = promotion.Redeem(Request(subtotal: 40_000m));

        Assert.Equal(4_000m, redemption.AppliedAmount);
        Assert.Equal(7, redemption.BookingId);
        Assert.Equal(3, redemption.RedeemedBy);
        Assert.Equal(Now, redemption.RedeemedAtUtc);
    }

    [Fact]
    public void Redeem_RaisesPromotionRedeemedEventWithTheAppliedAmount()
    {
        var promotion = Promotion.Create(Definition(value: 10m));

        promotion.Redeem(Request(subtotal: 40_000m));

        var redeemed = Assert.IsType<PromotionRedeemed>(Assert.Single(promotion.DomainEvents));
        Assert.Equal("VERANO10", redeemed.PromotionCode);
        Assert.Equal("RES-7", redeemed.BookingCode);
        Assert.Equal(3, redeemed.CustomerUserId);
        Assert.Equal(4_000m, redeemed.DiscountAmount);
        Assert.Equal(Now, redeemed.OccurredOnUtc);
    }

    [Fact]
    public void Redeem_WhenRejected_RaisesNoEvent()
    {
        var promotion = Promotion.Create(Definition(requiredPoints: 100));

        Assert.Throws<DomainException>(() => promotion.Redeem(Request(points: 0)));

        Assert.Empty(promotion.DomainEvents);
    }

    [Fact]
    public void Redeem_WithoutEnoughPoints_IsRejected()
    {
        var promotion = Promotion.Create(Definition(requiredPoints: 100));

        var error = Assert.Throws<DomainException>(() => promotion.Redeem(Request(points: 99)));

        Assert.Equal("PROMOTION_NOT_REDEEMABLE", error.Code);
    }

    [Fact]
    public void Redeem_WhenPaused_IsRejected()
    {
        var promotion = Promotion.Create(Definition());
        promotion.SetActive(false);

        Assert.Throws<DomainException>(() => promotion.Redeem(Request()));
    }

    [Fact]
    public void Redeem_TwiceOnSameBooking_IsRejected()
    {
        var promotion = Promotion.Create(Definition());

        var error = Assert.Throws<DomainException>(() => promotion.Redeem(Request(alreadyRedeemed: true)));

        Assert.Equal("PROMOTION_ALREADY_REDEEMED", error.Code);
    }

    [Fact]
    public void Redeem_BelowMinPurchase_IsRejected()
    {
        var promotion = Promotion.Create(Definition());
        SetLimit(promotion, nameof(Promotion.MinPurchaseAmount), 50_000m);

        var error = Assert.Throws<DomainException>(() => promotion.Redeem(Request(subtotal: 40_000m)));

        Assert.Equal("PROMOTION_MIN_PURCHASE_NOT_MET", error.Code);
    }

    [Fact]
    public void Redeem_WhenTotalLimitReached_IsRejected()
    {
        var promotion = Promotion.Create(Definition());
        SetLimit(promotion, nameof(Promotion.MaxRedemptions), 5);

        var error = Assert.Throws<DomainException>(() => promotion.Redeem(Request(total: 5)));

        Assert.Equal("PROMOTION_EXHAUSTED", error.Code);
    }

    [Fact]
    public void Redeem_WhenCustomerLimitReached_IsRejected()
    {
        var promotion = Promotion.Create(Definition());
        SetLimit(promotion, nameof(Promotion.MaxRedemptionsPerCustomer), 1);

        var error = Assert.Throws<DomainException>(() => promotion.Redeem(Request(byCustomer: 1)));

        Assert.Equal("PROMOTION_CUSTOMER_LIMIT_REACHED", error.Code);
    }

    [Fact]
    public void Redeem_WithNothingLeftToPay_IsRejected()
    {
        // booking_promotion exige applied_amount > 0 (ck_bpromo_amount).
        var promotion = Promotion.Create(Definition());

        var error = Assert.Throws<DomainException>(() => promotion.Redeem(Request(subtotal: 0m)));

        Assert.Equal("PROMOTION_NOTHING_TO_DISCOUNT", error.Code);
    }
}
