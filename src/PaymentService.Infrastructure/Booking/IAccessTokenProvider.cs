namespace PaymentService.Infrastructure.Booking;

/// <summary>
/// Da el token de acceso del usuario del request en curso, para reenviarlo a booking-service y que
/// este aplique sus propias reglas de acceso (un cliente solo ve sus reservas, /admin solo el
/// admin). Lo implementa el host (la Api, que conoce el request HTTP); así ni los casos de uso ni
/// esta capa dependen de ASP.NET.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>El token sin el prefijo "Bearer", o null si el request no trae uno.</summary>
    string? GetAccessToken();
}
