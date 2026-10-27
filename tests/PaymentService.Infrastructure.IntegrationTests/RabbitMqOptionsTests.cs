using Microsoft.Extensions.Configuration;
using PaymentService.Infrastructure.Messaging;
using Xunit;

namespace PaymentService.Infrastructure.IntegrationTests;

// Credenciales de RabbitMQ: guest/guest solo contra un RabbitMQ local (el único caso en que RabbitMQ lo
// acepta, igual que los servicios Java en local). Contra uno remoto, sin usuario y contraseña el
// servicio no arranca (antes caía en guest/guest sin avisar). No necesita base ni bus.
public sealed class RabbitMqOptionsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void From_Disabled_DoesNotRequireCredentials()
    {
        var options = RabbitMqOptions.From(Config(("RABBITMQ_HOST", "rabbitmq")));

        Assert.False(options.Enabled);
    }

    [Theory]
    [InlineData("rabbitmq", null, "secret")]
    [InlineData("rabbitmq", "user", null)]
    [InlineData("10.0.0.5", "", "")]
    public void From_EnabledAgainstARemoteBusWithoutCredentials_Throws(string host, string? username, string? password)
    {
        var config = Config(("MESSAGING_ENABLED", "true"), ("RABBITMQ_HOST", host),
            ("RABBITMQ_USERNAME", username), ("RABBITMQ_PASSWORD", password));

        var error = Assert.Throws<InvalidOperationException>(() => RabbitMqOptions.From(config));
        Assert.Contains("RABBITMQ_PASSWORD", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void From_EnabledAgainstALocalBusWithoutCredentials_UsesGuest(string? host)
    {
        var options = RabbitMqOptions.From(Config(("MESSAGING_ENABLED", "true"), ("RABBITMQ_HOST", host)));

        Assert.True(options.Enabled);
        Assert.Equal("guest", options.Username);
        Assert.Equal("guest", options.Password);
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
