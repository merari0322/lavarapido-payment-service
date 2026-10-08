using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Booking;
using Xunit;

namespace PaymentService.Infrastructure.IntegrationTests;

/// <summary>
/// Solo corre si PAYMENT_INTEGRATION_DB tiene una cadena de conexión a una base ya migrada
/// (por ejemplo la de "docker compose up migrate" en el puerto 1434). Nunca apuntes esta variable
/// a una base con datos reales: las pruebas insertan filas.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public const string Variable = "PAYMENT_INTEGRATION_DB";

    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Define {Variable} para correr las pruebas contra SQL Server.";
    }
}

/// <summary>
/// Arma la infraestructura real (EF Core + SQL Server) igual que Program.cs y carga los catálogos.
/// Cada "request" de una prueba es un scope nuevo: su propio DbContext, como en producción.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(IntegrationFactAttribute.Variable);
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PaymentDb"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAccessTokenProvider, NoToken>();
        services.AddInfrastructure(configuration);
        Services = services.BuildServiceProvider();

        await Services.InitializeInfrastructureAsync();
    }

    public Task DisposeAsync() => Services?.DisposeAsync().AsTask() ?? Task.CompletedTask;

    /// <summary>Un request nuevo (scope con su propio DbContext).</summary>
    public AsyncServiceScope NewRequest() => Services.CreateAsyncScope();

    /// <summary>Ids al azar para que cada corrida use sus propias reservas y clientes.</summary>
    public static long NewId() => Random.Shared.NextInt64(1_000_000_000, 9_000_000_000);

    private sealed class NoToken : IAccessTokenProvider
    {
        public string? GetAccessToken() => null;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
