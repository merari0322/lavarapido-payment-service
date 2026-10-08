using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Promotions;

namespace PaymentService.Api.Controllers;

public sealed record SavePromotionRequest(
    string Code,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    string? Icon,
    bool Featured,
    IReadOnlyList<string>? Benefits,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int DiscountPercent,
    int RequiredPoints);

public sealed record SetPromotionActiveRequest(bool Active);

/// <summary>Gestión de promociones (paquetes a precio fijo). Solo ADMIN; ver nota en PromotionApplicationService.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/v1/admin/promotions")]
public class PromotionsController : ControllerBase
{
    private readonly PromotionApplicationService _service;

    public PromotionsController(PromotionApplicationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IReadOnlyList<PromotionDto>> List(CancellationToken ct) => await _service.ListAsync(ct);

    [HttpGet("metrics")]
    public async Task<PromotionMetricsDto> Metrics(CancellationToken ct) => await _service.MetricsAsync(ct);

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> Create(SavePromotionRequest request, CancellationToken ct)
    {
        var promotion = await _service.CreateAsync(ToCommand(request), ct);
        return Created($"/api/v1/admin/promotions/{promotion.Id}", promotion);
    }

    [HttpPut("{id:int}")]
    public async Task<PromotionDto> Update(int id, SavePromotionRequest request, CancellationToken ct) =>
        await _service.UpdateAsync(id, ToCommand(request), ct);

    [HttpPatch("{id:int}/active")]
    public async Task<PromotionDto> SetActive(int id, SetPromotionActiveRequest request, CancellationToken ct) =>
        await _service.SetActiveAsync(id, request.Active, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, CallerOf.Request(HttpContext).UserId, ct);
        return NoContent();
    }

    private static SavePromotionCommand ToCommand(SavePromotionRequest r) =>
        new(r.Code, r.Name, r.Description, r.Price, r.DurationMinutes, r.Icon, r.Featured,
            r.Benefits ?? Array.Empty<string>(), r.ValidFrom, r.ValidTo, r.DiscountPercent, r.RequiredPoints);
}
