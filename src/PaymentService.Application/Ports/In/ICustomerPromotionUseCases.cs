using PaymentService.Application.Common;
using PaymentService.Application.Promotions;

namespace PaymentService.Application.Ports.In;

/// <summary>Puerto de entrada: promociones tal como las ve y las usa el cliente (catálogo vigente y canje al pagar).</summary>
public interface ICustomerPromotionUseCases
{
    /// <summary>Promociones vigentes hoy, marcando cuáles ya desbloqueó el cliente con sus puntos.</summary>
    Task<IReadOnlyList<PromotionForCustomerDto>> ListAvailableAsync(Caller caller, CancellationToken ct);

    /// <summary>Canjea un cupón en una reserva propia que todavía no tiene pago reportado ni aprobado.</summary>
    Task<RedeemPromotionResultDto> RedeemAsync(RedeemPromotionCommand command, Caller caller, CancellationToken ct);
}
