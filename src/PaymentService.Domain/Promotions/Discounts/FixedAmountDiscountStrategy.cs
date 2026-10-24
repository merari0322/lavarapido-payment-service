using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions.Discounts;

/// <summary>Estrategia FIXED: descuenta un monto fijo en pesos, sin importar el subtotal.</summary>
public sealed class FixedAmountDiscountStrategy : IDiscountStrategy
{
    public decimal Calculate(decimal subtotal, decimal discountValue) => discountValue;

    public void Validate(decimal discountValue) =>
        Guard.Against(discountValue <= 0, DomainErrorCodes.InvalidPromotionDiscount, "El descuento fijo debe ser mayor que cero.");
}
