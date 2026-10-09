namespace PaymentService.Application.Ports.Out.Integration;

/// <summary>
/// Puerto hacia booking-service, dueño de las reservas. El cliente solo ve sus reservas; el admin,
/// todas. Quién llama lo resuelve el adaptador (propaga la identidad del usuario del request), así
/// el caso de uso no maneja tokens. El monto a pagar sale de aquí, nunca del navegador.
/// Si booking-service no responde, el adaptador lanza ServiceUnavailableException.
/// </summary>
public interface IBookingDirectory
{
    /// <summary>La reserva si es del cliente que hace el request; null si no existe o no es suya.</summary>
    Task<BookingInfo?> GetForCustomerAsync(long bookingId, CancellationToken ct);

    /// <summary>Todas las reservas del cliente que hace el request.</summary>
    Task<IReadOnlyList<BookingInfo>> MineAsync(CancellationToken ct);

    /// <summary>Cualquier reserva (el request debe ser de un ADMIN); null si no existe.</summary>
    Task<BookingInfo?> GetForAdminAsync(long bookingId, CancellationToken ct);
}

/// <summary>
/// Lo que payment-service necesita saber de una reserva. Es un modelo interno del puerto: no sale
/// tal cual en las respuestas (para eso está PaymentBookingDto), así un cambio en booking-service
/// no rompe el contrato con la web.
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
