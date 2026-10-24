namespace PaymentService.Api.Contracts;

/// <summary>
/// Crear o editar una promoción. DiscountType (PERCENT o FIXED) y DiscountValue son opcionales:
/// si no vienen, el descuento es DiscountPercent por ciento (lo que manda la web actual).
/// Price y DurationMinutes son opcionales: la web y la app ya no los piden.
/// </summary>
public sealed record SavePromotionRequest(
    string Code,
    string Name,
    string? Description,
    decimal? Price,
    int? DurationMinutes,
    string? Icon,
    bool Featured,
    IReadOnlyList<string>? Benefits,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int DiscountPercent,
    int RequiredPoints,
    string? DiscountType = null,
    decimal? DiscountValue = null);

/// <summary>Pausar (false) o reanudar (true) una promoción.</summary>
public sealed record SetPromotionActiveRequest(bool Active);
