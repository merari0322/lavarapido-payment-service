using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Payments;

/// <summary>
/// Reúne los datos para calcular lo que falta por pagar de una reserva (su total y los cupones
/// canjeados) y le pide la cifra al dominio: cuánto descuenta hoy cada canje
/// (PromotionRedemption.DiscountsOn, que recalcula los porcentuales con el total actual, BIZ-05) y
/// la resta (PaymentPolicy.AmountDue). Lo usan el pago (monto esperado), el canje de cupones
/// (subtotal sobre el que se descuenta) y la pantalla de pago del cliente.
/// </summary>
public sealed class AmountDueCalculator
{
    private readonly IPromotionRedemptionRepository _redemptions;

    public AmountDueCalculator(IPromotionRedemptionRepository redemptions)
    {
        _redemptions = redemptions;
    }

    public async Task<decimal> ForAsync(BookingInfo booking, CancellationToken ct) =>
        (await BreakdownAsync(booking, ct)).AmountDue;

    /// <summary>La misma cifra con su desglose, para mostrarla antes de registrar el pago.</summary>
    public async Task<AmountDueDto> BreakdownAsync(BookingInfo booking, CancellationToken ct) =>
        (await DetailAsync(booking, ct)).Due;

    /// <summary>El desglose y lo que descuenta hoy cada canje (la pantalla de pago muestra el cupón con su monto).</summary>
    public async Task<AmountDueDetail> DetailAsync(BookingInfo booking, CancellationToken ct)
    {
        var redemptions = await _redemptions.ListForBookingAsync(booking.Id, ct);
        var discounts = PromotionRedemption.DiscountsOn(booking.Total, redemptions);
        var applied = discounts.Sum(d => d.Amount);
        return new AmountDueDetail(
            new AmountDueDto(booking.Id, booking.Total, applied, PaymentPolicy.AmountDue(booking.Total, applied)),
            discounts);
    }
}

/// <summary>Lo que falta por pagar y lo que descuenta hoy cada canje de la reserva.</summary>
public sealed record AmountDueDetail(AmountDueDto Due, IReadOnlyList<RedemptionDiscount> Discounts);
