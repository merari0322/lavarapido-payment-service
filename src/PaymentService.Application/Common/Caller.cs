namespace PaymentService.Application.Common;

/// <summary>
/// Quién ejecuta el caso de uso: el id de usuario autenticado (claim sub). La aplicación no conoce
/// el mecanismo de autenticación (JWT, token...): eso queda en los adaptadores.
/// </summary>
public sealed record Caller(long UserId);
