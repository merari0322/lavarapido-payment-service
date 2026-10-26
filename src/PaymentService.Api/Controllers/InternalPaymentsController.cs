using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Http;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Rutas entre servicios (/internal/**): solo con la llave interna (X-Internal-Key), nunca con el JWT
/// de un usuario. El api-gateway no las enruta; booking-service las llama directo por la red interna.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = InternalApiKeyAuthenticationHandler.Scheme)]
[Route("internal/v1/payments")]
public sealed class InternalPaymentsController : ControllerBase
{
    private readonly IPaymentQueryUseCases _queries;

    public InternalPaymentsController(IPaymentQueryUseCases queries) => _queries = queries;

    /// <summary>
    /// Si la reserva ya tiene un pago en revisión o aprobado. PaymentDeadlineJob de booking-service lo
    /// pregunta antes de cancelar por falta de pago, por si se perdió el evento payment.reported.
    /// </summary>
    [HttpGet("bookings/{bookingId:long}/reported")]
    public Task<BookingReportedDto> Reported(long bookingId, CancellationToken ct) =>
        _queries.BookingReportedAsync(bookingId, ct);
}
