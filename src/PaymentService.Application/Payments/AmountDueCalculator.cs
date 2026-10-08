using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;

namespace PaymentService.Application.Payments;

/// <summary>
/// Lo que realmente falta por pagar de una reserva: el total que dice booking-service menos lo que
/// ya descontaron los cupones canjeados en ella. Lo usan el pago (monto esperado) y el canje de
/// cupones (subtotal sobre el que se descuenta), así ambos calculan exactamente lo mismo.
/// </summary>
public sealed class AmountDueCalculator
{
    private readonly IPromotionRedemptionRepository _redemptions;

    public AmountDueCalculator(IPromotionRedemptionRepository redemptions)
    {
        _redemptions = redemptions;
    }

    public async Task<decimal> ForAsync(BookingInfo booking, CancellationToken ct)
    {
        var discounted = await _redemptions.AppliedDiscountTotalAsync(booking.Id, ct);
        return Math.Max(booking.Total - discounted, 0m);
    }
}
