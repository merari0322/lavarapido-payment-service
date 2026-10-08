namespace PaymentService.Domain.Loyalty;

/// <summary>
/// Tipos de movimiento de payment.loyalty_movement_type. El dominio los identifica por su código
/// (EARNED, REDEEMED...); el ID numérico de la tabla lo resuelve la infraestructura al arrancar,
/// porque SQL Server puede saltar valores IDENTITY y no conviene suponerlos fijos.
/// </summary>
public enum LoyaltyMovementType : short
{
    Earned = 1,
    Redeemed = 2,
    Expired = 3,
    Adjusted = 4
}

/// <summary>Código de la tabla y signo (columna sign) de cada tipo de movimiento.</summary>
public static class LoyaltyMovementTypeCatalog
{
    public static string ToCode(this LoyaltyMovementType type) => type switch
    {
        LoyaltyMovementType.Earned => "EARNED",
        LoyaltyMovementType.Redeemed => "REDEEMED",
        LoyaltyMovementType.Expired => "EXPIRED",
        _ => "ADJUSTED"
    };

    /// <summary>Si el movimiento suma (+1) o resta (-1) puntos al saldo.</summary>
    public static int Sign(this LoyaltyMovementType type) => type switch
    {
        LoyaltyMovementType.Redeemed or LoyaltyMovementType.Expired => -1,
        // ADJUSTED quedó sembrado con signo +1 (provisional en la migración 015).
        _ => 1
    };
}
