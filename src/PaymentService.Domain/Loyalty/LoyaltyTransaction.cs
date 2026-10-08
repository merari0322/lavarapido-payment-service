using PaymentService.Domain.Common;

namespace PaymentService.Domain.Loyalty;

/// <summary>
/// Un movimiento del ledger de puntos (tabla payment.loyalty_transaction). Es append-only: nunca se
/// edita una fila, se agrega otra. BalanceAfter (saldo justo después de este movimiento) es la
/// fuente de verdad del saldo: el saldo actual de un cliente es el BalanceAfter de su última fila,
/// y cualquier saldo pasado se reconstruye sin recorrer todo el historial.
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

    /// <summary>Reserva que originó el movimiento; null en ajustes manuales y vencimientos.</summary>
    public long? BookingId { get; private set; }

    /// <summary>Puntos con signo de este movimiento (nunca 0).</summary>
    public int Points { get; private set; }

    public int BalanceAfter { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public string? Description { get; private set; }
    public long? CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Factory Method: puntos ganados por una reserva pagada.</summary>
    public static LoyaltyTransaction Earn(long customerId, long bookingId, int points, int currentBalance, long? actor)
    {
        Guard.PositiveId(bookingId, "INVALID_LOYALTY_BOOKING", "Los puntos ganados deben venir de una reserva.");
        return Record(customerId, LoyaltyMovementType.Earned, bookingId, points, currentBalance,
            "Puntos ganados por reserva pagada", actor);
    }

    /// <summary>
    /// Arma el movimiento aplicando el signo de su tipo y calcula el saldo resultante. El saldo
    /// nunca puede quedar negativo (ck_loyalty_balance): se valida aquí con un mensaje claro en
    /// vez de esperar el error de la base.
    /// </summary>
    private static LoyaltyTransaction Record(long customerId, LoyaltyMovementType type, long? bookingId,
        int points, int currentBalance, string? description, long? actor)
    {
        Guard.PositiveId(customerId, "INVALID_LOYALTY_CUSTOMER", "El movimiento debe indicar el cliente.");
        Guard.Against(points <= 0, "INVALID_LOYALTY_POINTS", "La cantidad de puntos debe ser mayor que cero.");
        Guard.Against(currentBalance < 0, "INVALID_LOYALTY_BALANCE", "El saldo actual no puede ser negativo.");

        var signed = points * type.Sign();
        var balanceAfter = currentBalance + signed;
        Guard.Against(balanceAfter < 0, "LOYALTY_INSUFFICIENT_BALANCE", "El cliente no tiene puntos suficientes.");

        return new LoyaltyTransaction
        {
            CustomerId = customerId,
            MovementType = type,
            BookingId = bookingId,
            Points = signed,
            BalanceAfter = balanceAfter,
            Description = Guard.Optional(description, MaxDescriptionLength, "INVALID_LOYALTY_DESCRIPTION",
                $"La descripción no puede superar {MaxDescriptionLength} caracteres."),
            CreatedBy = actor,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
