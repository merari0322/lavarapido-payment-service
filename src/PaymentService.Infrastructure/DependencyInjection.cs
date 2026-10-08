using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Infrastructure.Booking;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Repositories;

namespace PaymentService.Infrastructure;

/// <summary>
/// Conecta cada puerto de salida de Application con su adaptador concreto. Es el único lugar que
/// sabe que la persistencia es EF Core + SQL Server, que booking se consulta por HTTP y que el bus
/// es RabbitMQ: cambiar cualquiera de ellos no toca ni el dominio ni los casos de uso.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        AddPersistence(services, configuration);
        AddBookingDirectory(services, configuration);
        AddMessaging(services, configuration);
        return services;
    }

    /// <summary>
    /// Paso de arranque: carga los IDs reales de los catálogos (CatalogIds) antes de atender
    /// requests. Si la base no responde el servicio no arranca: es preferible a leer o escribir
    /// tipos de descuento o de movimiento con un ID equivocado.
    /// </summary>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await CatalogIds.LoadAsync(db, ct);
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("PaymentDb")));

        // Scoped: todos comparten el DbContext del request, así la Unit of Work confirma todo junto.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentAccountRepository, PaymentAccountRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IPromotionRedemptionRepository, PromotionRedemptionRepository>();
        services.AddScoped<ILoyaltyLedgerRepository, LoyaltyLedgerRepository>();
    }

    private static void AddBookingDirectory(IServiceCollection services, IConfiguration configuration)
    {
        // HttpClient tipado (IHttpClientFactory): URL y tiempo de espera configurables.
        var bookingUrl = configuration["BOOKING_SERVICE_URL"] ?? "http://localhost:3003/api/v1";
        services.AddHttpClient<IBookingDirectory, BookingServiceDirectory>(http =>
        {
            http.BaseAddress = new Uri(bookingUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(5);
        });
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var options = RabbitMqOptions.From(configuration);
        if (options.Enabled)
        {
            services.AddSingleton(options);
            services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
        }
        else
        {
            services.AddSingleton<IIntegrationEventPublisher, NullIntegrationEventPublisher>();
        }
    }
}
