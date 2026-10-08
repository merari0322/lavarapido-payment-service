namespace PaymentService.Application.Common;

/// <summary>
/// Quién llama, leído del JWT ya verificado por la API: el id de usuario (claim sub) y el token
/// mismo, que se reenvía a booking-service para que aplique sus propias reglas de acceso.
/// </summary>
public sealed record Caller(long UserId, string BearerToken);
