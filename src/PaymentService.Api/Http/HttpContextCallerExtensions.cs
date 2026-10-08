using PaymentService.Application.Common;

namespace PaymentService.Api.Http;

/// <summary>
/// Adaptador de entrada: convierte el usuario autenticado del request HTTP en el Caller que
/// entienden los casos de uso (id del claim sub).
/// </summary>
internal static class HttpContextCallerExtensions
{
    public static Caller GetCaller(this HttpContext http)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (!long.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException("The token has no valid subject");
        return new Caller(userId);
    }
}
