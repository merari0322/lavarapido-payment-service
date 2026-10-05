using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Payments;
using PaymentService.Infrastructure.Booking;
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

        // booking-service: URL y tiempo de espera configurables (BOOKING_SERVICE_URL)
        var bookingUrl = configuration["BOOKING_SERVICE_URL"] ?? "http://localhost:3003/api/v1";
        services.AddHttpClient<IBookingDirectory, BookingServiceDirectory>(http =>
        {
            http.BaseAddress = new Uri(bookingUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
