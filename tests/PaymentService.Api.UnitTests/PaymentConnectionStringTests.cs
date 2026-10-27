using Microsoft.Extensions.Configuration;
using PaymentService.Api.Configuration;
using Xunit;

namespace PaymentService.Api.UnitTests;

/// <summary>
/// La cadena de conexión se arma con DB_* de lavarapido-infra/.env y nunca lleva una contraseña en el
/// código: sin DB_PASSWORD (y sin ConnectionStrings:PaymentDb) el servicio no arranca, como pasa con
/// JWT_SECRET, en vez de intentar conectar con la contraseña vacía.
/// </summary>
public sealed class PaymentConnectionStringTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Ensure_WithoutDbPassword_Throws()
    {
        var config = Config(("DB_HOST", "sqlserver"));

        var error = Assert.Throws<InvalidOperationException>(config.EnsurePaymentConnectionString);
        Assert.Contains("DB_PASSWORD", error.Message);
    }

    [Fact]
    public void Ensure_WithDbVariables_BuildsTheConnectionString()
    {
        var config = Config(("DB_HOST", "sqlserver"), ("DB_NAME", "LavaRapido"), ("DB_PASSWORD", "secreto"));

        config.EnsurePaymentConnectionString();

        var connection = config.GetConnectionString("PaymentDb");
        Assert.Contains("Server=sqlserver,1433;", connection);
        Assert.Contains("Database=LavaRapido;", connection);
        Assert.Contains("Password=secreto;", connection);
    }

    [Fact]
    public void Ensure_WithAnExplicitConnectionString_KeepsIt()
    {
        const string explicitConnection = "Server=otro;Database=Pruebas;Integrated Security=True";
        var config = Config(("ConnectionStrings:PaymentDb", explicitConnection));

        config.EnsurePaymentConnectionString();

        Assert.Equal(explicitConnection, config.GetConnectionString("PaymentDb"));
    }
}
