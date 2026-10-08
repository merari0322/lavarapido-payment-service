namespace PaymentService.Domain.Promotions.Discounts;

/// <summary>
/// Patrón Strategy: cada tipo de descuento (porcentaje, monto fijo) calcula a su manera cuánto se
/// descuenta de un subtotal. Promotion no necesita un switch por tipo: le pide la estrategia a
/// DiscountStrategyFactory y delega el cálculo. Agregar un tipo nuevo es agregar una clase, sin
/// tocar Promotion (principio abierto/cerrado).
/// </summary>
public interface IDiscountStrategy
{
    /// <summary>Descuento bruto, antes de aplicar topes (MaxDiscountAmount y el propio subtotal).</summary>
    decimal Calculate(decimal subtotal, decimal discountValue);

    /// <summary>Valida que DiscountValue tenga sentido para este tipo (lanza DomainException si no).</summary>
    void Validate(decimal discountValue);
}
