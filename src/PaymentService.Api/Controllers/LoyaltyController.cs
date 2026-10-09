using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Api.Http;
using PaymentService.Application.Loyalty;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Promotions;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Adaptador HTTP de entrada para la fidelización del cliente que llama: saldo de puntos,
/// promociones desbloqueadas y canje de cupones en la pantalla de pago. Las rutas se agrupan bajo
/// /loyalty por contrato con la web; los casos de uso son de dos módulos (Loyalty y Promotions).
/// </summary>
[ApiController]
[Authorize(Roles = "CLIENT")]
[Route("api/v1/loyalty")]
public sealed class LoyaltyController : ControllerBase
{
    private readonly ILoyaltyUseCases _loyalty;
    private readonly ICustomerPromotionUseCases _promotions;

    public LoyaltyController(ILoyaltyUseCases loyalty, ICustomerPromotionUseCases promotions)
    {
        _loyalty = loyalty;
        _promotions = promotions;
    }

    [HttpGet("balance")]
    public Task<LoyaltyBalanceDto> Balance(CancellationToken ct) =>
        _loyalty.BalanceAsync(HttpContext.GetCaller(), ct);

    /// <summary>Promociones vigentes, marcando cuáles ya desbloqueó el cliente con sus puntos.</summary>
    [HttpGet("promotions")]
    public Task<IReadOnlyList<PromotionForCustomerDto>> Promotions(CancellationToken ct) =>
        _promotions.ListAvailableAsync(HttpContext.GetCaller(), ct);

    /// <summary>Canjea un cupón en una reserva propia que todavía no tiene pago reportado.</summary>
    [HttpPost("redeem")]
    public Task<RedeemPromotionResultDto> Redeem(RedeemPromotionRequest request, CancellationToken ct) =>
        _promotions.RedeemAsync(new RedeemPromotionCommand(request.BookingId, request.Code), HttpContext.GetCaller(), ct);
}
