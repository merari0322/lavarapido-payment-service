using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;

namespace PaymentService.Api;

/// <summary>
/// Errores como RFC 9457 con "code", igual que los servicios Java: la web traduce
/// API_ERRORS.&lt;code&gt;. El detalle es para el desarrollador.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title, code) = exception switch
        {
            DomainException e when e.Code == "BOOKING_SERVICE_UNAVAILABLE"
                => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", e.Code),
            DomainException e when e.Code is "PAYMENT_ALREADY_REPORTED" or "PAYMENT_ALREADY_APPROVED"
                => (StatusCodes.Status409Conflict, "Conflict", e.Code),
            DomainException e => (StatusCodes.Status400BadRequest, "Bad Request", e.Code),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not Found", e.Code),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "UNAUTHORIZED"),
            _ => (0, string.Empty, string.Empty)
        };

        if (status == 0) return false;

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = exception.Message };
        problem.Extensions["code"] = code;
        await httpContext.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
