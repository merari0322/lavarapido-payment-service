using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Api.Http;
using PaymentService.Application.Loyalty;
using PaymentService.Application.Ports.In;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Adaptador HTTP de entrada para la fidelización del cliente que llama: saldo de puntos,
/// promociones desbloqueadas y canje de cupones en la pantalla de pago.
/// </summary>
[ApiController]
[Authorize(Roles = "CLIENT")]
[Route("api/v1/loyalty")]
public sealed class LoyaltyController : ControllerBase
{
    private readonly ILoyaltyUseCases _loyalty;

    public LoyaltyController(ILoyaltyUseCases loyalty)
    {
        _loyalty = loyalty;
    }

    [HttpGet("balance")]
    public async Task<BalanceResponse> Balance(CancellationToken ct) =>
        new(await _loyalty.BalanceAsync(HttpContext.GetCaller(), ct));

    /// <summary>Promociones vigentes, marcando cuáles ya desbloqueó el cliente con sus puntos.</summary>
    [HttpGet("promotions")]
    public Task<IReadOnlyList<PromotionForCustomerDto>> Promotions(CancellationToken ct) =>
        _loyalty.PromotionsForCustomerAsync(HttpContext.GetCaller(), ct);

    /// <summary>Canjea un cupón en una reserva propia que todavía no tiene pago reportado.</summary>
    [HttpPost("redeem")]
    public Task<RedeemPromotionResultDto> Redeem(RedeemPromotionRequest request, CancellationToken ct) =>
        _loyalty.RedeemAsync(request.BookingId, request.Code, HttpContext.GetCaller(), ct);
}
