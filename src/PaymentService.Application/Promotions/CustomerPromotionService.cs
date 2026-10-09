using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Promotions;

/// <summary>
/// Promociones del lado del cliente: las vigentes (marcando cuáles ya desbloqueó con sus puntos) y
/// el canje de un cupón al pagar. Canjear no gasta puntos (los puntos desbloquean, ver
/// LoyaltyService): el descuento queda como PromotionRedemption y AmountDueCalculator lo resta
/// del total cuando se paga.
/// </summary>
public sealed class CustomerPromotionService : ICustomerPromotionUseCases
{
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionRedemptionRepository _redemptions;
    private readonly ILoyaltyLedgerRepository _ledger;
    private readonly IBookingDirectory _bookings;
    private readonly BookingPaymentGuard _guard;
    private readonly AmountDueCalculator _amountDue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _events;
    private readonly TimeProvider _clock;

    public CustomerPromotionService(IPromotionRepository promotions, IPromotionRedemptionRepository redemptions,
        ILoyaltyLedgerRepository ledger, IBookingDirectory bookings, BookingPaymentGuard guard,
        AmountDueCalculator amountDue, IUnitOfWork unitOfWork, IIntegrationEventPublisher events, TimeProvider clock)
    {
        _promotions = promotions;
        _redemptions = redemptions;
        _ledger = ledger;
        _bookings = bookings;
        _guard = guard;
        _amountDue = amountDue;
        _unitOfWork = unitOfWork;
        _events = events;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromotionForCustomerDto>> ListAvailableAsync(Caller caller, CancellationToken ct)
    {
        var points = (await _ledger.CurrentBalanceAsync(caller.UserId, ct)).Points;
        var today = DateOnly.FromDateTime(Now);
        return (await _promotions.ListAsync(ct))
            .Where(p => p.IsAvailableOn(today))
            .Select(p => p.ToCustomerDto(points))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RedeemPromotionResultDto> RedeemAsync(RedeemPromotionCommand command, Caller caller,
        CancellationToken ct)
    {
        var booking = await _bookings.RequireForCustomerAsync(command.BookingId, ct);
        // Con un pago ya reportado o aprobado el monto quedó fijado: un cupón ahora no lo cambiaría.
        await _guard.EnsureBookingIsUnpaidAsync(booking, allowOpenPayment: false, ct);

        var promotion = (string.IsNullOrWhiteSpace(command.Code) ? null : await _promotions.GetByCodeAsync(command.Code, ct))
            ?? throw new NotFoundException(ErrorCodes.PromotionNotFound, "Ese cupón no existe.");

        // Las consultas van una por una: la persistencia no admite operaciones en paralelo.
        var subtotal = await _amountDue.ForAsync(booking, ct);
        var request = new RedemptionRequest(
            booking.Id,
            booking.Code,
            caller.UserId,
            Now,
            (await _ledger.CurrentBalanceAsync(caller.UserId, ct)).Points,
            subtotal,
            await _redemptions.ExistsAsync(booking.Id, promotion.Id, ct),
            await _redemptions.CountAsync(promotion.Id, ct),
            await _redemptions.CountByCustomerAsync(promotion.Id, caller.UserId, ct));

        // La regla completa del canje vive en el aggregate.
        var redemption = promotion.Redeem(request);
        _redemptions.Add(redemption);
        await _unitOfWork.CommitAsync(ct);
        await _events.PublishAndClearAsync(promotion, PromotionIntegrationEvents.From(promotion), ct);

        return new RedeemPromotionResultDto(promotion.Id, promotion.Code, promotion.Name, redemption.AppliedAmount,
            subtotal - redemption.AppliedAmount);
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;
}
