using PaymentService.Domain.Common;

namespace PaymentService.Domain.Loyalty;

/// <summary>
/// Value Object: el saldo de puntos de un cliente tal como lo deja su último movimiento del ledger,
/// junto con la posición (Sequence) de ese movimiento. El siguiente movimiento ocupa Sequence + 1;
/// como esa posición es única por cliente, dos movimientos calculados a la vez sobre el mismo saldo
/// no pueden guardarse los dos (el segundo choca y se reintenta con el saldo nuevo).
/// </summary>
public sealed record LoyaltyBalance
{
    public static readonly LoyaltyBalance Empty = new(0, 0);

    public LoyaltyBalance(int points, int sequence)
    {
        Guard.Against(points < 0, DomainErrorCodes.InvalidLoyaltyBalance, "El saldo de puntos no puede ser negativo.");
        Guard.Against(sequence < 0, DomainErrorCodes.InvalidLoyaltyBalance, "La posición en el ledger no puede ser negativa.");
        Points = points;
        Sequence = sequence;
    }

    /// <summary>Puntos disponibles.</summary>
    public int Points { get; }

    /// <summary>Posición del último movimiento del cliente en su ledger (0 = sin movimientos).</summary>
    public int Sequence { get; }
}
