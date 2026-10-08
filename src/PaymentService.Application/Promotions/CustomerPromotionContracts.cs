namespace PaymentService.Application.Promotions;

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

/// <summary>El cliente canjea un cupón (código de la promoción) en una de sus reservas.</summary>
public sealed record RedeemPromotionCommand(long BookingId, string Code);

/// <summary>Resultado de canjear un cupón: cuánto se descontó y cuánto queda por pagar.</summary>
public sealed record RedeemPromotionResultDto(
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    decimal DiscountAmount,
    decimal NewTotal);
