namespace PaymentService.Domain.Loyalty;

/// <summary>
/// Tipos de movimiento del ledger de puntos. El dominio los identifica por su código (EARNED,
/// REVERSED...); el ID de cada uno en su catálogo lo resuelve la infraestructura.
/// </summary>
public enum LoyaltyMovementType
{
    /// <summary>Puntos ganados por una reserva pagada.</summary>
    Earned,

    /// <summary>Puntos gastados (reservado; hoy los puntos desbloquean promociones, no se gastan).</summary>
    Redeemed,

    /// <summary>Puntos vencidos.</summary>
    Expired,

    /// <summary>Ajuste manual del admin.</summary>
    Adjusted,

    /// <summary>Puntos ganados que se revierten porque el pago de la reserva se reembolsó.</summary>
    Reversed
}

/// <summary>Código y signo de cada tipo de movimiento (lenguaje del negocio).</summary>
public static class LoyaltyMovementTypeCatalog
{
    public static string ToCode(this LoyaltyMovementType type) => type switch
    {
        LoyaltyMovementType.Earned => "EARNED",
        LoyaltyMovementType.Redeemed => "REDEEMED",
        LoyaltyMovementType.Expired => "EXPIRED",
        LoyaltyMovementType.Adjusted => "ADJUSTED",
        LoyaltyMovementType.Reversed => "REVERSED",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>Si el movimiento suma (+1) o resta (-1) puntos al saldo.</summary>
    public static int Sign(this LoyaltyMovementType type) => type switch
    {
        LoyaltyMovementType.Redeemed or LoyaltyMovementType.Expired or LoyaltyMovementType.Reversed => -1,
        _ => 1
    };
}
