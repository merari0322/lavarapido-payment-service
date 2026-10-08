using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Loyalty;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Promotions;

namespace PaymentService.Application;

/// <summary>
/// Inyección de dependencias de la capa de aplicación: cada puerto de entrada se registra con su
/// implementación, y los colaboradores internos (guardas, calculadoras, ensambladores) como
/// servicios concretos. Todo es Scoped porque depende de repositorios que viven por request.
/// La API solo llama a AddApplication(); no necesita conocer las clases concretas.
/// El reloj (TimeProvider) lo registra el composition root: es un recurso del host, no de una capa.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Puertos de entrada (los usan los controllers).
        services.AddScoped<IPaymentCommandUseCases, PaymentCommandService>();
        services.AddScoped<IPaymentQueryUseCases, PaymentQueryService>();
        services.AddScoped<IPaymentAccountUseCases, PaymentAccountService>();
        services.AddScoped<IPromotionAdminUseCases, PromotionAdminService>();
        services.AddScoped<ICustomerPromotionUseCases, CustomerPromotionService>();
        services.AddScoped<ILoyaltyUseCases, LoyaltyService>();

        // Colaboradores compartidos entre casos de uso.
        services.AddScoped<BookingPaymentGuard>();
        services.AddScoped<AmountDueCalculator>();
        services.AddScoped<PaymentDtoAssembler>();
        services.AddScoped<PaymentAccountReader>();
        services.AddScoped<LoyaltyRewardService>();
        return services;
    }
}
