using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Common;
using PaymentService.Application.Loyalty;
using PaymentService.Application.Payments;
using PaymentService.Application.Promotions;
using PaymentService.Infrastructure.Booking;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("PaymentDb")));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentAccountRepository, PaymentAccountRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
        services.AddSingleton(TimeProvider.System);

        // booking-service: URL y tiempo de espera configurables (BOOKING_SERVICE_URL)
        var bookingUrl = configuration["BOOKING_SERVICE_URL"] ?? "http://localhost:3003/api/v1";
        services.AddHttpClient<IBookingDirectory, BookingServiceDirectory>(http =>
        {
            http.BaseAddress = new Uri(bookingUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(5);
        });

        // carwash.events (RabbitMQ): mismas variables y el mismo "apagado por defecto" que los
        // servicios en Java (@ConditionalOnProperty app.messaging.enabled)
        var messagingEnabled = string.Equals(Environment.GetEnvironmentVariable("MESSAGING_ENABLED"), "true",
            StringComparison.OrdinalIgnoreCase);
        if (messagingEnabled)
        {
            services.AddSingleton<IDomainEventPublisher>(sp => new RabbitDomainEventPublisher(
                sp.GetRequiredService<ILogger<RabbitDomainEventPublisher>>(),
                Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port : 5672,
                Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
                Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest"));
        }
        else
        {
            services.AddSingleton<IDomainEventPublisher, NullDomainEventPublisher>();
        }

        return services;
    }
}
