namespace PaymentService.Application.Ports.Out.Integration;

/// <summary>
/// Puerto hacia booking-service, dueño de las reservas (REST con el token del usuario, ADR-004).
/// El cliente solo ve sus reservas; el admin, todas. El monto a pagar sale de aquí, nunca del
/// navegador. Si booking-service no responde, el adaptador lanza ServiceUnavailableException.
/// </summary>
public interface IBookingDirectory
{
    /// <summary>La reserva si es del cliente dueño del token; null si no existe o no es suya.</summary>
    Task<BookingInfo?> GetForCustomerAsync(long bookingId, string bearerToken, CancellationToken ct);

    /// <summary>Todas las reservas del cliente dueño del token.</summary>
    Task<IReadOnlyList<BookingInfo>> MineAsync(string bearerToken, CancellationToken ct);

    /// <summary>Cualquier reserva (requiere token de ADMIN); null si no existe.</summary>
    Task<BookingInfo?> GetForAdminAsync(long bookingId, string bearerToken, CancellationToken ct);
}

/// <summary>
/// Lo que payment-service necesita saber de una reserva. También viaja tal cual en la respuesta de
/// los pagos (la web lo muestra), por eso conserva estos nombres de campo.
/// </summary>
public sealed record BookingInfo(
    long Id,
    string Code,
    string Status,
    decimal Total,
    string Date,
    string StartTime,
    string Services,
    string Vehicle,
    string Plate,
    long? OwnerUserId,
    int TotalLoyaltyPoints);
