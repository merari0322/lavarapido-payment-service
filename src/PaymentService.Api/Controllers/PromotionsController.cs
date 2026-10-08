using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Api.Http;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Promotions;

namespace PaymentService.Api.Controllers;

/// <summary>Adaptador HTTP de entrada para la gestión de promociones. Solo ADMIN.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/v1/admin/promotions")]
public sealed class PromotionsController : ControllerBase
{
    private readonly IPromotionUseCases _promotions;

    public PromotionsController(IPromotionUseCases promotions)
    {
        _promotions = promotions;
    }

    [HttpGet]
    public Task<IReadOnlyList<PromotionDto>> List(CancellationToken ct) => _promotions.ListAsync(ct);

    [HttpGet("metrics")]
    public Task<PromotionMetricsDto> Metrics(CancellationToken ct) => _promotions.MetricsAsync(ct);

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> Create(SavePromotionRequest request, CancellationToken ct)
    {
        var promotion = await _promotions.CreateAsync(ToCommand(request), ct);
        return Created($"/api/v1/admin/promotions/{promotion.Id}", promotion);
    }

    [HttpPut("{id:int}")]
    public Task<PromotionDto> Update(int id, SavePromotionRequest request, CancellationToken ct) =>
        _promotions.UpdateAsync(id, ToCommand(request), ct);

    [HttpPatch("{id:int}/active")]
    public Task<PromotionDto> SetActive(int id, SetPromotionActiveRequest request, CancellationToken ct) =>
        _promotions.SetActiveAsync(id, request.Active, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _promotions.DeleteAsync(id, HttpContext.GetCaller().UserId, ct);
        return NoContent();
    }

    private static SavePromotionCommand ToCommand(SavePromotionRequest r) =>
        new(r.Code, r.Name, r.Description, r.Price, r.DurationMinutes, r.Icon, r.Featured,
            r.Benefits ?? Array.Empty<string>(), r.ValidFrom, r.ValidTo, r.DiscountPercent, r.RequiredPoints,
            r.DiscountType, r.DiscountValue);
}
