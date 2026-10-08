using System.Net.Http.Headers;

namespace PaymentService.Infrastructure.Booking;

/// <summary>
/// DelegatingHandler del HttpClient de booking-service: agrega a cada llamada el token del usuario
/// del request en curso (propagación de identidad). Así BookingServiceDirectory y los casos de uso
/// no reciben ni pasan tokens.
/// </summary>
internal sealed class AccessTokenForwardingHandler : DelegatingHandler
{
    private readonly IAccessTokenProvider _tokens;

    public AccessTokenForwardingHandler(IAccessTokenProvider tokens)
    {
        _tokens = tokens;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_tokens.GetAccessToken() is { Length: > 0 } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
