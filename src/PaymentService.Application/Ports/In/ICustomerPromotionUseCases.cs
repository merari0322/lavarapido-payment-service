using PaymentService.Application.Common;
using PaymentService.Application.Payments;
using PaymentService.Application.Promotions;

namespace PaymentService.Application.Ports.In;

/// <summary>Puerto de entrada: promociones tal como las ve y las usa el cliente (catálogo vigente y canje al pagar).</summary>
public interface ICustomerPromotionUseCases
{
    /// <summary>Promociones vigentes hoy, marcando cuáles ya desbloqueó el cliente con sus puntos.</summary>
    Task<IReadOnlyList<PromotionForCustomerDto>> ListAvailableAsync(Caller caller, CancellationToken ct);

    /// <summary>Canjea un cupón en una reserva propia que todavía no tiene pago reportado ni aprobado.</summary>
    Task<RedeemPromotionResultDto> RedeemAsync(RedeemPromotionCommand command, Caller caller, CancellationToken ct);

    /// <summary>
    /// Lo que falta por pagar de una reserva propia y el cupón que tiene aplicado (BIZ-02), para que
    /// la pantalla de pago muestre el monto del servidor aunque se recargue.
    /// </summary>
    Task<BookingRedemptionDto> GetRedemptionAsync(long bookingId, Caller caller, CancellationToken ct);

    /// <summary>
    /// Anula el canje de un cupón en una reserva propia ("Quitar cupón", E-08) mientras no tenga un pago
    /// en revisión ni aprobado, y devuelve lo que ahora falta por pagar.
    /// </summary>
    Task<AmountDueDto> CancelRedemptionAsync(long bookingId, int promotionId, Caller caller, CancellationToken ct);
}
