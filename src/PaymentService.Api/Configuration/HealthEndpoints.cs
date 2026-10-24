using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Api.Configuration;

/// <summary>
/// Salud de payment-service. Antes respondía UP siempre; ahora consulta SQL Server, así Docker,
/// el gateway y quien revise el servicio ven DOWN (503) cuando la base no responde.
/// </summary>
internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (PaymentDbContext db, CancellationToken ct) =>
            await CanReachDatabase(db, ct)
                ? Results.Ok(new { service = "payment-service", status = "ok" })
                : Results.Json(new { service = "payment-service", status = "down" },
                    statusCode: StatusCodes.Status503ServiceUnavailable));

        // misma ruta y forma que los servicios Java (Spring Boot Actuator)
        app.MapGet("/actuator/health", async (PaymentDbContext db, CancellationToken ct) =>
            await CanReachDatabase(db, ct)
                ? Results.Ok(new { status = "UP" })
                : Results.Json(new { status = "DOWN" }, statusCode: StatusCodes.Status503ServiceUnavailable));
    }

    // un chequeo de salud debe contestar rápido aunque la base no responda
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private static async Task<bool> CanReachDatabase(PaymentDbContext db, CancellationToken ct)
    {
        try
        {
            // SqlClient no siempre respeta la cancelación al abrir la conexión: se espera como
            // máximo Timeout y, si la base no contesta, se responde DOWN sin esperar más
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var check = db.Database.CanConnectAsync(limit.Token);
            var winner = await Task.WhenAny(check, Task.Delay(Timeout, ct));
            if (winner != check)
            {
                limit.Cancel();
                return false;
            }
            return await check;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
