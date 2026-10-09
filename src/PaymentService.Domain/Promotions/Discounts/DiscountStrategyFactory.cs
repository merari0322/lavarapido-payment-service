using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions.Discounts;

/// <summary>
/// Patrón Factory: el único lugar que sabe qué estrategia corresponde a cada DiscountType. Las
/// estrategias no tienen estado, así que se reutiliza una sola instancia de cada una.
/// </summary>
public static class DiscountStrategyFactory
{
    private static readonly IDiscountStrategy Percentage = new PercentageDiscountStrategy();
    private static readonly IDiscountStrategy FixedAmount = new FixedAmountDiscountStrategy();

    public static IDiscountStrategy For(DiscountType type) => type switch
    {
        DiscountType.Percentage => Percentage,
        DiscountType.FixedAmount => FixedAmount,
        // PACKAGE guardaba un precio, no un descuento: calcularlo regalaría la reserva completa.
        _ => throw new DomainException(DomainErrorCodes.PromotionDiscountUnsupported,
            "Esta promoción usa un tipo de descuento que ya no se admite; el administrador debe editarla.")
    };
}
