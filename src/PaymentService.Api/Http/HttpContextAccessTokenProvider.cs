using PaymentService.Infrastructure.Booking;

namespace PaymentService.Api.Http;

/// <summary>
/// Implementa IAccessTokenProvider con el request HTTP en curso: devuelve el token del header
/// Authorization (ya validado por JwtBearer) para que Infrastructure lo reenvíe a booking-service.
/// Usa IHttpContextAccessor porque el handler del HttpClient no vive en el scope del request.
/// </summary>
internal sealed class HttpContextAccessTokenProvider : IAccessTokenProvider
{
    private const string BearerPrefix = "Bearer ";

    private readonly IHttpContextAccessor _http;

    public HttpContextAccessTokenProvider(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? GetAccessToken()
    {
        var header = _http.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return null;
        return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ? header[BearerPrefix.Length..] : header;
    }
}
