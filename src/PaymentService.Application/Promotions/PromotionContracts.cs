namespace PaymentService.Application.Promotions;

/// <summary>
/// Promoción para la pantalla del admin. Status (active / scheduled / paused) se calcula, no se
/// guarda. DiscountType y DiscountValue se agregaron al contrato sin quitar DiscountPercent, que
/// la web ya usa.
/// </summary>
public sealed record PromotionDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    decimal? Price,
    int? DurationMinutes,
    string? Icon,
    bool Featured,
    IReadOnlyList<string> Benefits,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Status,
    int Redemptions,
    int DiscountPercent,
    int RequiredPoints,
    string DiscountType,
    decimal DiscountValue);

/// <summary>
/// Métricas del panel. Conversion queda en 0: nada registra cuántas veces se mostró una
/// promoción, así que todavía no hay con qué calcularla.
/// </summary>
public sealed record PromotionMetricsDto(int Redemptions, decimal Savings, decimal Conversion);

/// <summary>
/// Datos para crear o editar una promoción. DiscountType (PERCENT o FIXED) y DiscountValue son
/// opcionales por compatibilidad: si no vienen, el descuento es DiscountPercent por ciento, que es
/// lo que manda la web actual. Price y DurationMinutes también son opcionales (referencia del
/// paquete, el cupón no los usa).
/// </summary>
public sealed record SavePromotionCommand(
    string Code,
    string Name,
    string? Description,
    decimal? Price,
    int? DurationMinutes,
    string? Icon,
    bool Featured,
    IReadOnlyList<string> Benefits,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int DiscountPercent,
    int RequiredPoints,
    string? DiscountType,
    decimal? DiscountValue);
