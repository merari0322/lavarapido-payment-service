using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentService.Application.Loyalty;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Promotions;

namespace PaymentService.Application;

/// <summary>
/// Inyección de dependencias de la capa de aplicación: cada puerto de entrada se registra con su
/// implementación, y los colaboradores internos (guardas, calculadoras, fábricas de vistas) como
/// servicios concretos. Todo es Scoped porque depende de repositorios que viven por request.
/// La API solo llama a AddApplication(); no necesita conocer las clases concretas.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Puertos de entrada (los usan los controllers).
        services.AddScoped<IPaymentCommands, PaymentCommandService>();
        services.AddScoped<IPaymentQueries, PaymentQueryService>();
        services.AddScoped<IPaymentAccountUseCases, PaymentAccountService>();
        services.AddScoped<IPromotionUseCases, PromotionService>();
        services.AddScoped<ILoyaltyUseCases, LoyaltyService>();

        // Colaboradores compartidos entre casos de uso.
        services.AddScoped<BookingPaymentGuard>();
        services.AddScoped<AmountDueCalculator>();
        services.AddScoped<PaymentViewFactory>();
        services.AddScoped<PaymentAccountReader>();
        services.AddScoped<LoyaltyRewardService>();

        // Reloj inyectable: las pruebas pueden fijar "hoy" sin tocar el código.
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
