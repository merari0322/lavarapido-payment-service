using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions;

/// <summary>
/// Tipos de descuento de una promoción. El dominio los identifica por su código (PERCENT, FIXED,
/// PACKAGE); el ID de cada uno en su catálogo lo resuelve la infraestructura.
/// </summary>
public enum DiscountType
{
    /// <summary>Porcentaje sobre el subtotal (DiscountValue entre 0 y 100).</summary>
    Percentage,

    /// <summary>Monto fijo en pesos.</summary>
    FixedAmount,

    /// <summary>
    /// Tipo histórico: guardaba el precio del paquete en DiscountValue. Ya no se crea; las
    /// promociones que lo conserven no se pueden canjear hasta que el admin las edite.
    /// </summary>
    Package
}

/// <summary>Traducción entre el enum y su código (PERCENT, FIXED, PACKAGE).</summary>
public static class DiscountTypeCodes
{
    public static string ToCode(this DiscountType type) => type switch
    {
        DiscountType.Percentage => "PERCENT",
        DiscountType.FixedAmount => "FIXED",
        _ => "PACKAGE"
    };

    /// <summary>Convierte el código recibido; solo PERCENT y FIXED son válidos para crear o editar.</summary>
    public static DiscountType ParseEditable(string code) => code.Trim().ToUpperInvariant() switch
    {
        "PERCENT" => DiscountType.Percentage,
        "FIXED" => DiscountType.FixedAmount,
        _ => throw new DomainException(DomainErrorCodes.InvalidDiscountType, $"Tipo de descuento desconocido: {code}. Use PERCENT o FIXED.")
    };
}
