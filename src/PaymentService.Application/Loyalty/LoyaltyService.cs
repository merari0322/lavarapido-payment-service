using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Application.Promotions;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Saldo de puntos del cliente, promociones que ya desbloqueó y canje de un cupón en el pago.
///
/// Los puntos desbloquean la promoción, no se gastan (RF-026: "al llegar a ciertos porcentajes con
/// los puntos acumulados se le van desbloqueando los beneficios o promociones"), por eso canjear
/// no escribe un movimiento REDEEMED en el ledger. El descuento se registra en booking_promotion y
/// AmountDueCalculator lo resta del total cuando se paga.
/// </summary>
public sealed class LoyaltyService : ILoyaltyUseCases
{
    private readonly ILoyaltyLedgerRepository _ledger;
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionRedemptionRepository _redemptions;
    private readonly IBookingDirectory _bookings;
    private readonly BookingPaymentGuard _guard;
    private readonly AmountDueCalculator _amountDue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _events;
    private readonly TimeProvider _clock;

    public LoyaltyService(ILoyaltyLedgerRepository ledger, IPromotionRepository promotions,
        IPromotionRedemptionRepository redemptions, IBookingDirectory bookings, BookingPaymentGuard guard,
        AmountDueCalculator amountDue, IUnitOfWork unitOfWork, IIntegrationEventPublisher events, TimeProvider clock)
    {
        _ledger = ledger;
        _promotions = promotions;
        _redemptions = redemptions;
        _bookings = bookings;
        _guard = guard;
        _amountDue = amountDue;
        _unitOfWork = unitOfWork;
        _events = events;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<int> BalanceAsync(Caller caller, CancellationToken ct) =>
        _ledger.CurrentBalanceAsync(caller.UserId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromotionForCustomerDto>> PromotionsForCustomerAsync(Caller caller, CancellationToken ct)
    {
        var balance = await _ledger.CurrentBalanceAsync(caller.UserId, ct);
        var today = Today;
        return (await _promotions.ListAsync(ct))
            .Where(p => p.IsAvailableOn(today))
            .Select(p => p.ToCustomerDto(balance))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RedeemPromotionResultDto> RedeemAsync(long bookingId, string code, Caller caller, CancellationToken ct)
    {
        var booking = await _bookings.RequireForCustomerAsync(bookingId, caller, ct);
        // Con un pago ya reportado o aprobado el monto quedó fijado: un cupón ahora no lo cambiaría.
        await _guard.EnsureBookingIsUnpaidAsync(booking, allowOpenPayment: false, ct);

        var promotion = (string.IsNullOrWhiteSpace(code) ? null : await _promotions.GetByCodeAsync(code, ct))
            ?? throw new NotFoundException(ErrorCodes.PromotionNotFound, "Ese cupón no existe.");

        // Las consultas van una por una: el DbContext no admite operaciones en paralelo.
        var subtotal = await _amountDue.ForAsync(booking, ct);
        var request = new RedemptionRequest(
            booking.Id,
            caller.UserId,
            Today,
            await _ledger.CurrentBalanceAsync(caller.UserId, ct),
            subtotal,
            await _redemptions.ExistsAsync(booking.Id, promotion.Id, ct),
            await _redemptions.CountAsync(promotion.Id, ct),
            await _redemptions.CountByCustomerAsync(promotion.Id, caller.UserId, ct));

        // La regla completa del canje vive en el aggregate.
        var redemption = promotion.Redeem(request);
        _redemptions.Add(redemption);
        await _unitOfWork.CommitAsync(ct);

        await _events.PublishAsync(PromotionRedeemedEvent(booking, promotion, redemption.AppliedAmount, caller.UserId), ct);
        return new RedeemPromotionResultDto(promotion.Id, promotion.Code, promotion.Name, redemption.AppliedAmount,
            subtotal - redemption.AppliedAmount);
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    /// <summary>Evento para notification-service (aviso in-app y correo del canje).</summary>
    private static IntegrationEvent PromotionRedeemedEvent(BookingInfo booking, Promotion promotion, decimal discount,
        long customerUserId) =>
        new("PromotionRedeemed", "payment.promotion_redeemed", booking.Id.ToString(), new Dictionary<string, object?>
        {
            ["customerUserId"] = customerUserId,
            ["bookingId"] = booking.Id,
            ["bookingCode"] = booking.Code,
            ["promotionCode"] = promotion.Code,
            ["promotionName"] = promotion.Name,
            ["discountAmount"] = discount
        });
}
