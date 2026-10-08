using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Loyalty;
using PaymentService.Application.Promotions;

namespace PaymentService.Api.Controllers;

public sealed record BalanceResponse(int Points);

public sealed record RedeemPromotionRequest(long BookingId, string Code);

/// <summary>Puntos de fidelización y canje de cupones del cliente que llama (dashboard y pantalla de pago).</summary>
[ApiController]
[Authorize(Roles = "CLIENT")]
[Route("api/v1/loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly LoyaltyApplicationService _service;

    public LoyaltyController(LoyaltyApplicationService service)
    {
        _service = service;
    }

    [HttpGet("balance")]
    public async Task<BalanceResponse> Balance(CancellationToken ct) =>
        new(await _service.BalanceAsync(CallerOf.Request(HttpContext), ct));

    /// <summary>Promociones vigentes, marcando cuáles ya desbloqueó el cliente con sus puntos.</summary>
    [HttpGet("promotions")]
    public async Task<IReadOnlyList<PromotionForCustomerDto>> Promotions(CancellationToken ct) =>
        await _service.PromotionsForCustomerAsync(CallerOf.Request(HttpContext), ct);

    /// <summary>Canjea un cupón en la pantalla de pago de una reserva propia.</summary>
    [HttpPost("redeem")]
    public async Task<RedeemPromotionResultDto> Redeem(RedeemPromotionRequest request, CancellationToken ct) =>
        await _service.RedeemAsync(request.BookingId, request.Code, CallerOf.Request(HttpContext), ct);
}
