using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PaymentService.Api.Http;

/// <summary>
/// Autentica las llamadas entre servicios a /internal/** con la llave compartida INTERNAL_API_KEY
/// que viaja en el encabezado X-Internal-Key, igual que InternalApiKeyFilter de booking-service y
/// security-service. Si la llave no llega, no coincide o no está configurada, la petición no se
/// autentica y responde 401. Comparación en tiempo constante. Nunca acepta el JWT de un usuario.
/// </summary>
internal sealed class InternalApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "InternalApiKey";
    public const string Header = "X-Internal-Key";
    public const string ConfigKey = "INTERNAL_API_KEY";

    private readonly byte[]? _expectedKey;

    public InternalApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
        : base(options, logger, encoder)
    {
        var key = config[ConfigKey];
        _expectedKey = string.IsNullOrWhiteSpace(key) ? null : Encoding.UTF8.GetBytes(key);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var provided) || string.IsNullOrEmpty(provided))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (_expectedKey is null
            || !CryptographicOperations.FixedTimeEquals(_expectedKey, Encoding.UTF8.GetBytes(provided.ToString())))
            return Task.FromResult(AuthenticateResult.Fail("Llave interna inválida."));

        var identity = new ClaimsIdentity(new[] { new Claim("sub", "internal-service") }, Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
