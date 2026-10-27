using Microsoft.Extensions.Configuration;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Conexión a RabbitMQ, con las mismas variables que los servicios en Java (lavarapido-infra/.env).
/// Apagado por defecto, igual que @ConditionalOnProperty app.messaging.enabled en Java, para no
/// intentar conectar donde RabbitMQ no está levantado. Sin usuario ni contraseña solo se usa guest/guest
/// contra un RabbitMQ en la misma máquina (localhost), el único caso en que RabbitMQ acepta guest, igual
/// que el valor por defecto de los servicios Java en local. Contra un RabbitMQ remoto (Docker, servidor)
/// exige RABBITMQ_USERNAME y RABBITMQ_PASSWORD: antes caía en guest/guest sin avisar.
/// </summary>
internal sealed record RabbitMqOptions(bool Enabled, string Host, int Port, string Username, string Password)
{
    private const string LocalDefault = "guest";

    public static RabbitMqOptions From(IConfiguration configuration)
    {
        var enabled = string.Equals(configuration["MESSAGING_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
        var host = configuration["RABBITMQ_HOST"] ?? "localhost";
        var username = configuration["RABBITMQ_USERNAME"];
        var password = configuration["RABBITMQ_PASSWORD"];
        var missing = string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password);

        if (enabled && missing && !IsLocal(host))
            throw new InvalidOperationException(
                $"MESSAGING_ENABLED=true with RABBITMQ_HOST={host} requires RABBITMQ_USERNAME and RABBITMQ_PASSWORD (lavarapido-infra/.env)");

        return new RabbitMqOptions(
            Enabled: enabled,
            Host: host,
            Port: int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            Username: missing ? LocalDefault : username!,
            Password: missing ? LocalDefault : password!);
    }

    private static bool IsLocal(string host) =>
        host is "localhost" or "127.0.0.1" or "::1";
}
