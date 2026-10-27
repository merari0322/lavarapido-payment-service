using Microsoft.Extensions.Configuration;
using PaymentService.Infrastructure.Messaging;
using Xunit;

namespace PaymentService.Infrastructure.IntegrationTests;

// Sin credenciales por defecto en el código: con la mensajería encendida, RabbitMQ exige usuario y
// contraseña (antes caía en guest/guest sin avisar). Apagada, no los pide. No necesita base ni bus.
public sealed class RabbitMqOptionsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void From_Disabled_DoesNotRequireCredentials()
    {
        var options = RabbitMqOptions.From(Config());

        Assert.False(options.Enabled);
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("user", null)]
    [InlineData("", "")]
    public void From_EnabledWithoutCredentials_Throws(string? username, string? password)
    {
        var config = Config(("MESSAGING_ENABLED", "true"), ("RABBITMQ_USERNAME", username), ("RABBITMQ_PASSWORD", password));

        var error = Assert.Throws<InvalidOperationException>(() => RabbitMqOptions.From(config));
        Assert.Contains("RABBITMQ_PASSWORD", error.Message);
    }

    [Fact]
    public void From_EnabledWithCredentials_UsesThem()
    {
        var config = Config(("MESSAGING_ENABLED", "true"), ("RABBITMQ_HOST", "rabbitmq"),
            ("RABBITMQ_USERNAME", "payment"), ("RABBITMQ_PASSWORD", "secret"));

        var options = RabbitMqOptions.From(config);

        Assert.True(options.Enabled);
        Assert.Equal("rabbitmq", options.Host);
        Assert.Equal(5672, options.Port);
        Assert.Equal("payment", options.Username);
        Assert.Equal("secret", options.Password);
    }
}
