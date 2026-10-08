using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions.Discounts;

/// <summary>Estrategia PERCENT: descuenta un porcentaje del subtotal (100 = gratis).</summary>
public sealed class PercentageDiscountStrategy : IDiscountStrategy
{
    public decimal Calculate(decimal subtotal, decimal discountValue) => subtotal * discountValue / 100m;

    public void Validate(decimal discountValue) =>
        Guard.Against(discountValue is <= 0 or > 100, "INVALID_PROMOTION_DISCOUNT",
            "El descuento porcentual debe estar entre 1 y 100.");
}
