using PaymentService.Application.Common;
using PaymentService.Application.Loyalty;

namespace PaymentService.Application.Ports.In;

/// <summary>Puerto de entrada: puntos de fidelización del cliente y canje de cupones al pagar.</summary>
public interface ILoyaltyUseCases
{
    /// <summary>Saldo actual de puntos del cliente que llama.</summary>
    Task<int> BalanceAsync(Caller caller, CancellationToken ct);

    /// <summary>Promociones vigentes hoy, marcando cuáles ya desbloqueó el cliente.</summary>
    Task<IReadOnlyList<PromotionForCustomerDto>> PromotionsForCustomerAsync(Caller caller, CancellationToken ct);

    /// <summary>Canjea un cupón en una reserva propia que todavía no tiene pago reportado ni aprobado.</summary>
    Task<RedeemPromotionResultDto> RedeemAsync(long bookingId, string code, Caller caller, CancellationToken ct);
}
