using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions.Discounts;

/// <summary>
/// Estrategia PERCENT: descuenta un porcentaje del subtotal, de 1 a 99. No hay 100 %: el pago exige un
/// monto mayor que cero, así que una reserva gratis no se podría pagar (E-07).
/// </summary>
public sealed class PercentageDiscountStrategy : IDiscountStrategy
{
    public const decimal MaxPercent = 99m;

    public decimal Calculate(decimal subtotal, decimal discountValue) => subtotal * discountValue / 100m;

    public void Validate(decimal discountValue) =>
        Guard.Against(discountValue is <= 0 or > MaxPercent, DomainErrorCodes.InvalidPromotionDiscount,
            $"El descuento porcentual debe estar entre 1 y {MaxPercent}.");
}
