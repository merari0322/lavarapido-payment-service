using PaymentService.Domain.Common;

namespace PaymentService.Domain.Loyalty;

/// <summary>
/// Un movimiento del ledger de puntos. Es append-only: nunca se
/// edita un movimiento, se agrega otro. BalanceAfter (saldo justo después de este movimiento) es la
/// fuente de verdad del saldo: el saldo actual de un cliente es el BalanceAfter de su último
/// movimiento (el de mayor Sequence), y cualquier saldo pasado se reconstruye sin recorrer todo el
/// historial.
///
/// CustomerId guarda el id de usuario (security.app_user) que booking-service expone como
/// ownerUserId: es el único identificador de cliente que los demás servicios pueden resolver hoy.
/// </summary>
public sealed class LoyaltyTransaction : Entity<long>
{
    private const int MaxDescriptionLength = 200;

    private LoyaltyTransaction() { }

    public long CustomerId { get; private set; }
    public LoyaltyMovementType MovementType { get; private set; }

    /// <summary>Posición del movimiento en el ledger del cliente (1, 2, 3...); única por cliente.</summary>
    public int Sequence { get; private set; }

    /// <summary>Reserva que originó el movimiento; null en ajustes manuales y vencimientos.</summary>
    public long? BookingId { get; private set; }

    /// <summary>Puntos con signo de este movimiento (nunca 0).</summary>
    public int Points { get; private set; }

    public int BalanceAfter { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public string? Description { get; private set; }
    public long? CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Saldo que deja este movimiento, listo para encadenar el siguiente.</summary>
    public LoyaltyBalance ResultingBalance => new(BalanceAfter, Sequence);

    /// <summary>Factory Method: puntos ganados por una reserva pagada.</summary>
    public static LoyaltyTransaction Earn(long customerId, long bookingId, int points, LoyaltyBalance current,
        long? actor, DateTime nowUtc)
    {
        Guard.PositiveId(bookingId, DomainErrorCodes.InvalidLoyaltyBooking, "Los puntos ganados deben venir de una reserva.");
        return Record(customerId, LoyaltyMovementType.Earned, bookingId, points, current,
            "Puntos ganados por reserva pagada", actor, nowUtc);
    }

    /// <summary>
    /// Factory Method: revierte los puntos que ganó una reserva cuyo pago se reembolsó. Si el
    /// cliente ya no tiene todos esos puntos (vencieron), se revierte solo lo que queda: el saldo
    /// nunca queda negativo. Devuelve null si no hay nada que revertir.
    /// </summary>
    public static LoyaltyTransaction? ReverseEarned(long customerId, long bookingId, int earnedPoints,
        LoyaltyBalance current, long? actor, DateTime nowUtc)
    {
        Guard.PositiveId(bookingId, DomainErrorCodes.InvalidLoyaltyBooking, "La reversión debe indicar la reserva.");
        var points = Math.Min(earnedPoints, current.Points);
        if (points <= 0) return null;

        return Record(customerId, LoyaltyMovementType.Reversed, bookingId, points, current,
            "Puntos revertidos por reembolso del pago", actor, nowUtc);
    }

    /// <summary>
    /// Arma el movimiento aplicando el signo de su tipo, calcula el saldo resultante y ocupa la
    /// siguiente posición del ledger. El saldo nunca puede quedar negativo.
    /// </summary>
    private static LoyaltyTransaction Record(long customerId, LoyaltyMovementType type, long? bookingId,
        int points, LoyaltyBalance current, string? description, long? actor, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(current);
        Guard.PositiveId(customerId, DomainErrorCodes.InvalidLoyaltyCustomer, "El movimiento debe indicar el cliente.");
        Guard.Against(points <= 0, DomainErrorCodes.InvalidLoyaltyPoints, "La cantidad de puntos debe ser mayor que cero.");

        var signed = points * type.Sign();
        var balanceAfter = current.Points + signed;
        Guard.Against(balanceAfter < 0, DomainErrorCodes.LoyaltyInsufficientBalance, "El cliente no tiene puntos suficientes.");

        return new LoyaltyTransaction
        {
            CustomerId = customerId,
            MovementType = type,
            Sequence = current.Sequence + 1,
            BookingId = bookingId,
            Points = signed,
            BalanceAfter = balanceAfter,
            Description = Guard.Optional(description, MaxDescriptionLength, DomainErrorCodes.InvalidLoyaltyDescription,
                $"La descripción no puede superar {MaxDescriptionLength} caracteres."),
            CreatedBy = actor,
            CreatedAtUtc = nowUtc
        };
    }
}
