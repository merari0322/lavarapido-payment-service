using PaymentService.Application.Common;

namespace PaymentService.Api.Http;

/// <summary>
/// Adaptador de entrada: convierte el usuario autenticado del request HTTP en el Caller que
/// entienden los casos de uso (id del claim sub + token para reenviarlo a booking-service).
/// </summary>
internal static class HttpContextCallerExtensions
{
    private const string BearerPrefix = "Bearer ";

    public static Caller GetCaller(this HttpContext http)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (!long.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException("The token has no valid subject");

        var header = http.Request.Headers.Authorization.ToString();
        var token = header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ? header[BearerPrefix.Length..] : header;
        return new Caller(userId, token);
    }
}
