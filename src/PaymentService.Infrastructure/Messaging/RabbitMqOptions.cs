using Microsoft.Extensions.Configuration;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Conexión a RabbitMQ, con las mismas variables que los servicios en Java (lavarapido-infra/.env).
/// Apagado por defecto, igual que @ConditionalOnProperty app.messaging.enabled en Java, para no
/// intentar conectar donde RabbitMQ no está levantado.
/// </summary>
internal sealed record RabbitMqOptions(bool Enabled, string Host, int Port, string Username, string Password)
{
    public static RabbitMqOptions From(IConfiguration configuration) => new(
        Enabled: string.Equals(configuration["MESSAGING_ENABLED"], "true", StringComparison.OrdinalIgnoreCase),
        Host: configuration["RABBITMQ_HOST"] ?? "localhost",
        Port: int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
        Username: configuration["RABBITMQ_USERNAME"] ?? "guest",
        Password: configuration["RABBITMQ_PASSWORD"] ?? "guest");
}
