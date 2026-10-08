using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Errors;

/// <summary>
/// Traduce las excepciones de las capas internas a respuestas RFC 9457 (ProblemDetails) con un
/// campo "code", igual que los servicios Java: la web traduce API_ERRORS.&lt;code&gt; y Detail es
/// para el desarrollador. El código HTTP se decide por el TIPO de excepción, no por su texto:
///   DomainException (regla de un aggregate) y InvalidRequestException → 400,
///   NotFoundException → 404, ConflictException → 409, ServiceUnavailableException → 503,
///   UnauthorizedAccessException → 401.
/// Cualquier otra excepción sigue al manejador por defecto (500, sin detalles internos).
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var mapped = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, "Bad Request", e.Code),
            InvalidRequestException e => (StatusCodes.Status400BadRequest, "Bad Request", e.Code),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not Found", e.Code),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Code),
            ServiceUnavailableException e => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", e.Code),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "UNAUTHORIZED"),
            _ => ((int Status, string Title, string Code)?)null
        };
        if (mapped is not var (status, title, code)) return false;

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = exception.Message };
        problem.Extensions["code"] = code;
        await httpContext.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
