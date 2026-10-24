namespace PaymentService.Infrastructure.Booking;

/// <summary>
/// X-Correlation-Id de la petición en curso, para reenviarlo a booking-service y poder seguir una
/// acción del usuario entre servicios (igual que los servicios Java, cross-cutting.md §4).
/// La implementación vive en la API, que es la que conoce el request HTTP.
/// </summary>
public interface IRequestCorrelation
{
    string? CorrelationId { get; }
}
