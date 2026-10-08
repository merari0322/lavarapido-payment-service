using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Promotions;

/// <summary>
/// Gestión de promociones por parte del admin: catálogo (crear, editar, pausar, borrar) y las
/// cifras de uso, que se derivan de los canjes registrados.
/// </summary>
public sealed class PromotionAdminService : IPromotionAdminUseCases
{
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public PromotionAdminService(IPromotionRepository promotions, IPromotionRedemptionRepository redemptions,
        IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _promotions = promotions;
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromotionDto>> ListAsync(CancellationToken ct)
    {
        var promotions = await _promotions.ListAsync(ct);
        var stats = await _redemptions.StatsByPromotionAsync(ct);
        return promotions.Select(p => p.ToDto(stats.GetValueOrDefault(p.Id, RedemptionStats.None), Today)).ToList();
    }

    /// <inheritdoc />
    public async Task<PromotionMetricsDto> MetricsAsync(CancellationToken ct)
    {
        var stats = (await _redemptions.StatsByPromotionAsync(ct)).Values;
        return new PromotionMetricsDto(stats.Sum(s => s.Redemptions), stats.Sum(s => s.Savings), 0);
    }

    /// <inheritdoc />
    public async Task<PromotionDto> CreateAsync(SavePromotionCommand command, CancellationToken ct)
    {
        await EnsureCodeAvailableAsync(command.Code, null, ct);
        var promotion = Promotion.Create(command.ToDefinition());

        _promotions.Add(promotion);
        await _unitOfWork.CommitAsync(ct);
        return promotion.ToDto(RedemptionStats.None, Today);
    }

    /// <inheritdoc />
    public async Task<PromotionDto> UpdateAsync(int id, SavePromotionCommand command, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, ct);
        await EnsureCodeAvailableAsync(command.Code, id, ct);
        promotion.Update(command.ToDefinition());

        await _unitOfWork.CommitAsync(ct);
        return await ToDtoAsync(promotion, ct);
    }

    /// <inheritdoc />
    public async Task<PromotionDto> SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, ct);
        promotion.SetActive(active);

        await _unitOfWork.CommitAsync(ct);
        return await ToDtoAsync(promotion, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, Caller admin, CancellationToken ct)
    {
        var promotion = await LoadAsync(id, ct);
        _promotions.Remove(promotion, admin.UserId);
        await _unitOfWork.CommitAsync(ct);
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    private async Task EnsureCodeAvailableAsync(string code, int? exceptId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(code) && await _promotions.ExistsCodeAsync(code, exceptId, ct))
            throw new ConflictException(ErrorCodes.PromotionCodeTaken, $"Ya existe una promoción con el código {code}.");
    }

    private async Task<Promotion> LoadAsync(int id, CancellationToken ct) =>
        await _promotions.GetByIdAsync(id, ct)
        ?? throw new NotFoundException(ErrorCodes.PromotionNotFound, $"No existe la promoción {id}.");

    private async Task<PromotionDto> ToDtoAsync(Promotion promotion, CancellationToken ct)
    {
        var stats = await _redemptions.StatsByPromotionAsync(ct);
        return promotion.ToDto(stats.GetValueOrDefault(promotion.Id, RedemptionStats.None), Today);
    }
}
