using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>Repository del aggregate Promotion (las borradas no se ven).</summary>
public interface IPromotionRepository
{
    Task<IReadOnlyList<Promotion>> ListAsync(CancellationToken ct);

    Task<Promotion?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Busca por código sin distinguir mayúsculas (los códigos se guardan en mayúscula).</summary>
    Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>Si otra promoción (distinta de exceptId) ya usa ese código.</summary>
    Task<bool> ExistsCodeAsync(string code, int? exceptId, CancellationToken ct);

    void Add(Promotion promotion);

    /// <summary>
    /// Borrado lógico (deleted_at / deleted_by): la fila se conserva porque booking_promotion
    /// guarda el histórico de canjes que apunta a ella.
    /// </summary>
    void Remove(Promotion promotion, long deletedBy);
}
