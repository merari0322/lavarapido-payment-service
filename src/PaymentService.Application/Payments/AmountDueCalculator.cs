using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Reúne los datos para calcular lo que falta por pagar de una reserva (su total y lo ya
/// descontado por cupones) y le pide la cifra a la regla del dominio (PaymentPolicy.AmountDue).
/// Lo usan el pago (monto esperado) y el canje de cupones (subtotal sobre el que se descuenta).
/// </summary>
public sealed class AmountDueCalculator
{
    private readonly IPromotionRedemptionRepository _redemptions;

    public AmountDueCalculator(IPromotionRedemptionRepository redemptions)
    {
        _redemptions = redemptions;
    }

    public async Task<decimal> ForAsync(BookingInfo booking, CancellationToken ct) =>
        PaymentPolicy.AmountDue(booking.Total, await _redemptions.AppliedDiscountTotalAsync(booking.Id, ct));
}
