using PaymentService.Infrastructure.Booking;

namespace PaymentService.Api.Http;

/// <summary>
/// Toma el X-Correlation-Id que manda el gateway (o crea uno si no llega), lo devuelve en la
/// respuesta y lo pone en el contexto de los logs, igual que el CorrelationIdFilter de los
/// servicios Java. Así una acción del usuario se puede seguir de punta a punta.
/// </summary>
internal sealed class CorrelationIdMiddleware
{
    internal const string Header = "X-Correlation-Id";
    internal const string ItemKey = "CorrelationId";
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _log;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        // un valor raro o muy largo no se copia a los logs: se reemplaza por uno nuevo
        var id = IsValid(incoming) ? incoming : Guid.NewGuid().ToString();
        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });
        using (_log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await _next(context);
        }
    }

    internal static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxLength
        && value.All(c => char.IsLetterOrDigit(c) || c == '-');
}

/// <summary>Correlation id del request en curso para los clientes HTTP de Infrastructure.</summary>
internal sealed class HttpContextRequestCorrelation : IRequestCorrelation
{
    private readonly IHttpContextAccessor _http;

    public HttpContextRequestCorrelation(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? CorrelationId => _http.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;
}
