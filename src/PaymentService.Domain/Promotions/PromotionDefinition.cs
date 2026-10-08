namespace PaymentService.Domain.Promotions;

/// <summary>
/// Parameter Object con todo lo que el admin define de una promoción. Evita que Create y Update
/// reciban trece parámetros sueltos y garantiza que ambos validen exactamente los mismos datos.
/// </summary>
public sealed record PromotionDefinition(
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
    DiscountType DiscountType,
    decimal DiscountValue,
    int RequiredPoints);
