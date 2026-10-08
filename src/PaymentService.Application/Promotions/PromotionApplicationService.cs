using PaymentService.Application.Common;
using PaymentService.Domain.Common;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Promotions;

/// <summary>
/// Gestión de promociones (admin). "Redimir una promoción al pagar" no está aquí todavía: por
/// ahora esto es el catálogo (crear, editar, activar/pausar); Redemptions y Savings ya leen
/// promotion.booking_promotion, solo que nadie escribe ahí hasta que exista ese flujo.
/// </summary>
public class PromotionApplicationService
{
    private readonly IPromotionRepository _promotions;
    private readonly TimeProvider _clock;

    public PromotionApplicationService(IPromotionRepository promotions, TimeProvider clock)
    {
        _promotions = promotions;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PromotionDto>> ListAsync(CancellationToken ct)
    {
        var promotions = await _promotions.ListAsync(ct);
        var stats = await _promotions.RedemptionStatsAsync(ct);
        return promotions.Select(p => ToDto(p, stats)).ToList();
    }

    public async Task<PromotionMetricsDto> MetricsAsync(CancellationToken ct)
    {
        var stats = await _promotions.RedemptionStatsAsync(ct);
        var redemptions = stats.Values.Sum(s => s.Redemptions);
        var savings = stats.Values.Sum(s => s.Savings);
        // no hay nada que cuente cuántas veces se mostró/intentó una promoción, así que la
        // conversión real todavía no se puede calcular (ver nota de la clase)
        return new PromotionMetricsDto(redemptions, savings, 0);
    }

    public async Task<PromotionDto> CreateAsync(SavePromotionCommand command, CancellationToken ct)
    {
        if (await _promotions.ExistsCodeAsync(command.Code, null, ct))
            throw new DomainException("PROMOTION_CODE_TAKEN", $"Ya existe una promoción con el código {command.Code}.");

        var promotion = Promotion.Create(command.Code, command.Name, command.Description, command.Price,
            command.DurationMinutes, command.Icon, command.Featured, command.Benefits, command.ValidFrom, command.ValidTo,
            command.DiscountPercent, command.RequiredPoints);
        await _promotions.AddAsync(promotion, ct);
        await _promotions.SaveChangesAsync(ct);
        return ToDto(promotion, EmptyStats);
    }

    public async Task<PromotionDto> UpdateAsync(int id, SavePromotionCommand command, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, ct);
        if (await _promotions.ExistsCodeAsync(command.Code, id, ct))
            throw new DomainException("PROMOTION_CODE_TAKEN", $"Ya existe una promoción con el código {command.Code}.");

        promotion.Update(command.Code, command.Name, command.Description, command.Price, command.DurationMinutes,
            command.Icon, command.Featured, command.Benefits, command.ValidFrom, command.ValidTo,
            command.DiscountPercent, command.RequiredPoints);
        await _promotions.SyncDiscountColumnsAsync(promotion, ct);
        await _promotions.SaveChangesAsync(ct);
        return ToDto(promotion, await _promotions.RedemptionStatsAsync(ct));
    }

    public async Task<PromotionDto> SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, ct);
        promotion.SetActive(active);
        await _promotions.SaveChangesAsync(ct);
        return ToDto(promotion, await _promotions.RedemptionStatsAsync(ct));
    }

    public async Task DeleteAsync(int id, long actor, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, ct);
        await _promotions.SoftDeleteAsync(promotion, actor, ct);
        await _promotions.SaveChangesAsync(ct);
    }

    private static readonly IReadOnlyDictionary<int, (int Redemptions, decimal Savings)> EmptyStats =
        new Dictionary<int, (int Redemptions, decimal Savings)>();

    private async Task<Promotion> LoadAsync(int id, CancellationToken ct) =>
        await _promotions.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("PROMOTION_NOT_FOUND", $"No existe la promoción {id}.");

    private PromotionDto ToDto(Promotion p, IReadOnlyDictionary<int, (int Redemptions, decimal Savings)> stats)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var (redemptions, _) = stats.GetValueOrDefault(p.Id);
        return new PromotionDto(p.Id, p.Code, p.Name, p.Description, p.Price, p.DurationMinutes, p.Icon,
            p.Featured, p.Benefits, p.ValidFrom, p.ValidTo, p.StatusFor(today), redemptions,
            p.DiscountPercent, p.RequiredPoints);
    }
}
