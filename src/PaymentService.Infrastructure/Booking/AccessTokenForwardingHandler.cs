using System.Net.Http.Headers;

namespace PaymentService.Infrastructure.Booking;

/// <summary>
/// DelegatingHandler del HttpClient de booking-service: agrega a cada llamada el token del usuario
/// del request en curso (propagación de identidad) y su X-Correlation-Id. Así BookingServiceDirectory
/// y los casos de uso no reciben ni pasan tokens.
/// </summary>
internal sealed class AccessTokenForwardingHandler : DelegatingHandler
{
    internal const string CorrelationHeader = "X-Correlation-Id";

    private readonly IAccessTokenProvider _tokens;
    private readonly IRequestCorrelation _correlation;

    public AccessTokenForwardingHandler(IAccessTokenProvider tokens, IRequestCorrelation correlation)
    {
        _tokens = tokens;
        _correlation = correlation;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_tokens.GetAccessToken() is { Length: > 0 } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (_correlation.CorrelationId is { Length: > 0 } correlationId && !request.Headers.Contains(CorrelationHeader))
            request.Headers.Add(CorrelationHeader, correlationId);
        return base.SendAsync(request, ct);
    }
}
