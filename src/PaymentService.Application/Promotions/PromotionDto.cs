namespace PaymentService.Application.Promotions;

/// <summary>status: active, scheduled o paused (se calcula, no se guarda aparte).</summary>
public sealed record PromotionDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    string? Icon,
    bool Featured,
    IReadOnlyList<string> Benefits,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Status,
    int Redemptions,
    int DiscountPercent,
    int RequiredPoints);

public sealed record PromotionMetricsDto(int Redemptions, decimal Savings, decimal Conversion);

public sealed record SavePromotionCommand(
    string Code,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    string? Icon,
    bool Featured,
    IReadOnlyList<string> Benefits,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int DiscountPercent,
    int RequiredPoints);

/// <summary>Lo que ve el cliente de una promoción desbloqueada o no: para la pantalla de pago y el dashboard de fidelización.</summary>
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

/// <summary>Resultado de canjear un cupón en el pago: cuánto se descontó y qué promoción fue.</summary>
public sealed record RedeemPromotionResultDto(
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    decimal DiscountAmount,
    decimal NewTotal);
