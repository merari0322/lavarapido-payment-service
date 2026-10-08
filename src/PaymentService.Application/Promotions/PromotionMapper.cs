using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Promotions;

namespace PaymentService.Application.Promotions;

/// <summary>
/// Traducciones entre Promotion y los contratos de la aplicación (comandos y DTOs). Están juntas
/// para que el admin y el cliente vean siempre los mismos datos calculados de la misma forma.
/// </summary>
internal static class PromotionMapper
{
    /// <summary>
    /// Convierte el comando en la definición del dominio. Compatibilidad con la web actual: si no
    /// indica DiscountType, el descuento es DiscountPercent por ciento.
    /// </summary>
    public static PromotionDefinition ToDefinition(this SavePromotionCommand c)
    {
        var type = string.IsNullOrWhiteSpace(c.DiscountType)
            ? DiscountType.Percentage
            : DiscountTypeCodes.ParseEditable(c.DiscountType);
        var value = c.DiscountValue ?? c.DiscountPercent;

        return new PromotionDefinition(c.Code, c.Name, c.Description, c.Price, c.DurationMinutes, c.Icon, c.Featured,
            c.Benefits, c.ValidFrom, c.ValidTo, type, value, c.RequiredPoints);
    }

    public static PromotionDto ToDto(this Promotion p, RedemptionStats stats, DateOnly today) =>
        new(p.Id, p.Code, p.Name, p.Description, p.Price, p.DurationMinutes, p.Icon, p.Featured, p.Benefits,
            p.ValidFrom, p.ValidTo, p.StatusFor(today), stats.Redemptions, p.DiscountPercent, p.RequiredPoints,
            p.DiscountType.ToCode(), p.DiscountValue);

    public static PromotionForCustomerDto ToCustomerDto(this Promotion p, int customerPoints) =>
        new(p.Id, p.Code, p.Name, p.Description, p.Icon, p.Featured, p.Benefits, p.DiscountPercent,
            p.RequiredPoints, p.IsUnlockedFor(customerPoints));
}
