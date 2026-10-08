using PaymentService.Application.Promotions;

namespace PaymentService.Application.Ports.In;

/// <summary>Puerto de entrada: gestión de promociones por parte del admin.</summary>
public interface IPromotionUseCases
{
    Task<IReadOnlyList<PromotionDto>> ListAsync(CancellationToken ct);

    Task<PromotionMetricsDto> MetricsAsync(CancellationToken ct);

    Task<PromotionDto> CreateAsync(SavePromotionCommand command, CancellationToken ct);

    Task<PromotionDto> UpdateAsync(int id, SavePromotionCommand command, CancellationToken ct);

    /// <summary>Pausa o reanuda la promoción.</summary>
    Task<PromotionDto> SetActiveAsync(int id, bool active, CancellationToken ct);

    /// <summary>Borrado lógico; el histórico de canjes se conserva.</summary>
    Task DeleteAsync(int id, long deletedBy, CancellationToken ct);
}
