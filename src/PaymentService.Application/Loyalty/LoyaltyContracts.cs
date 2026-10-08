namespace PaymentService.Application.Loyalty;

/// <summary>Promoción vigente tal como la ve el cliente, marcando si ya la desbloqueó con sus puntos.</summary>
public sealed record PromotionForCustomerDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    string? Icon,
    bool Featured,
    IReadOnlyList<string> Benefits,
    int DiscountPercent,
    int RequiredPoints,
    bool Unlocked);

/// <summary>Resultado de canjear un cupón: cuánto se descontó y cuánto queda por pagar.</summary>
public sealed record RedeemPromotionResultDto(
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    decimal DiscountAmount,
    decimal NewTotal);
