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

/// <summary>
/// Lo que el cliente debe pagar hoy de una reserva y el cupón que tiene aplicado (BIZ-02). La web y
/// la app lo piden al abrir la pantalla de pago, así muestran el total del servidor aunque se
/// recargue la página. Los cuatro primeros campos son los mismos de AmountDueDto; Redemption es
/// null si la reserva no tiene cupón (con E-09 hay como máximo uno).
/// </summary>
public sealed record BookingRedemptionDto(
    long BookingId,
    decimal BookingTotal,
    decimal AppliedDiscounts,
    decimal AmountDue,
    AppliedRedemptionDto? Redemption);

/// <summary>El cupón aplicado a una reserva y cuánto descuenta.</summary>
public sealed record AppliedRedemptionDto(
    int PromotionId,
    string PromotionCode,
    string PromotionName,
    decimal DiscountAmount);
