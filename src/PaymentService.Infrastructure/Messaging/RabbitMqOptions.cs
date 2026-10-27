using Microsoft.Extensions.Configuration;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Conexión a RabbitMQ, con las mismas variables que los servicios en Java (lavarapido-infra/.env).
/// Apagado por defecto, igual que @ConditionalOnProperty app.messaging.enabled en Java, para no
/// intentar conectar donde RabbitMQ no está levantado. Encendido, exige usuario y contraseña: no hay
/// credenciales por defecto en el código (antes caía en guest/guest sin avisar).
/// </summary>
internal sealed record RabbitMqOptions(bool Enabled, string Host, int Port, string Username, string Password)
{
    public static RabbitMqOptions From(IConfiguration configuration)
    {
        var enabled = string.Equals(configuration["MESSAGING_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
        var username = configuration["RABBITMQ_USERNAME"];
        var password = configuration["RABBITMQ_PASSWORD"];
        if (enabled && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
            throw new InvalidOperationException(
                "MESSAGING_ENABLED=true requires RABBITMQ_USERNAME and RABBITMQ_PASSWORD (lavarapido-infra/.env)");

        return new RabbitMqOptions(
            Enabled: enabled,
            Host: configuration["RABBITMQ_HOST"] ?? "localhost",
            Port: int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            Username: username ?? string.Empty,
            Password: password ?? string.Empty);
    }
}
