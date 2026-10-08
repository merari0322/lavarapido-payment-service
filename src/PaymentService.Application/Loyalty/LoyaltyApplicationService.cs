using PaymentService.Application.Common;
using PaymentService.Application.Payments;
using PaymentService.Application.Promotions;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Saldo de puntos del cliente, promociones desbloqueadas y canje de un cupón en el pago.
///
/// Puntos ganados: PaymentApplicationService los acredita cuando aprueba el pago (ApproveAsync /
/// RegisterManualAsync), con CatalogItem.loyaltyPoints de cada servicio (booking-service), sumado
/// por booking.totalLoyaltyPoints.
///
/// Puntos canjeados: desbloquean la promoción, no se gastan (RF-026: "al llegar a ciertos
/// porcentajes con los puntos acomulados se le van desbloqueando los beneficios o promociones").
/// El descuento real (DiscountPercent de la promoción) se resta del total al reportar o registrar
/// el pago (ver PaymentApplicationService, ILoyaltyRepository.AppliedDiscountsAsync).
/// </summary>
public class LoyaltyApplicationService
{
    private readonly ILoyaltyRepository _loyalty;
    private readonly IPromotionRepository _promotions;
    private readonly IBookingDirectory _bookings;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _clock;

    public LoyaltyApplicationService(ILoyaltyRepository loyalty, IPromotionRepository promotions,
        IBookingDirectory bookings, IDomainEventPublisher events, TimeProvider clock)
    {
        _loyalty = loyalty;
        _promotions = promotions;
        _bookings = bookings;
        _events = events;
        _clock = clock;
    }

    public Task<int> BalanceAsync(Caller caller, CancellationToken ct) =>
        _loyalty.CurrentBalanceAsync(caller.UserId, ct);

    /// <summary>Todas las promociones vigentes hoy, marcando cuáles ya desbloqueó este cliente con sus puntos.</summary>
    public async Task<IReadOnlyList<PromotionForCustomerDto>> PromotionsForCustomerAsync(Caller caller, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var balance = await _loyalty.CurrentBalanceAsync(caller.UserId, ct);
        var promotions = await _promotions.ListAsync(ct);
        return promotions
            .Where(p => p.IsActive && today >= p.ValidFrom && today <= p.ValidTo)
            .Select(p => new PromotionForCustomerDto(p.Id, p.Code, p.Name, p.Description, p.Icon, p.Featured,
                p.Benefits, p.DiscountPercent, p.RequiredPoints, balance >= p.RequiredPoints))
            .ToList();
    }

    /// <summary>
    /// Canjea un cupón para una reserva del cliente que llama: valida vigencia, puntos y que no se
    /// haya canjeado antes en esa misma reserva; aplica el descuento y notifica (in-app + correo,
    /// ADR-011, vía carwash.events).
    /// </summary>
    public async Task<RedeemPromotionResultDto> RedeemAsync(long bookingId, string code, Caller caller, CancellationToken ct)
    {
        var booking = await _bookings.GetForCustomerAsync(bookingId, caller.BearerToken, ct)
            ?? throw new NotFoundException("BOOKING_NOT_FOUND", $"No existe la reserva {bookingId}.");

        var promotion = await _promotions.GetByCodeAsync(code, ct)
            ?? throw new NotFoundException("PROMOTION_NOT_FOUND", "Ese cupón no existe.");

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var balance = await _loyalty.CurrentBalanceAsync(caller.UserId, ct);
        if (!promotion.IsRedeemableToday(today, balance))
            throw new DomainException("PROMOTION_NOT_REDEEMABLE",
                "Ese cupón no está vigente o todavía no acumulas los puntos necesarios para desbloquearlo.");
        if (await _loyalty.AlreadyRedeemedAsync(bookingId, promotion.Id, ct))
            throw new DomainException("PROMOTION_ALREADY_REDEEMED", "Ya canjeaste este cupón en esta reserva.");

        var alreadyApplied = await _loyalty.AppliedDiscountsAsync(bookingId, ct);
        var remainingTotal = Math.Max(booking.Total - alreadyApplied, 0m);
        var discount = Math.Min(promotion.DiscountFor(remainingTotal), remainingTotal);

        await _loyalty.RecordRedemptionAsync(bookingId, promotion.Id, discount, caller.UserId, ct);
        await _loyalty.SaveChangesAsync(ct);

        await _events.PublishAsync("PromotionRedeemed", bookingId.ToString(), new Dictionary<string, object?>
        {
            ["customerUserId"] = caller.UserId,
            ["bookingId"] = bookingId,
            ["bookingCode"] = booking.Code,
            ["promotionCode"] = promotion.Code,
            ["promotionName"] = promotion.Name,
            ["discountAmount"] = discount,
        }, ct);

        return new RedeemPromotionResultDto(promotion.Id, promotion.Code, promotion.Name, discount,
            remainingTotal - discount);
    }
}
