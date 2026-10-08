using PaymentService.Application.Common;
using PaymentService.Application.Loyalty;

namespace PaymentService.Application.Ports.In;

/// <summary>Puerto de entrada: puntos de fidelización del cliente.</summary>
public interface ILoyaltyUseCases
{
    /// <summary>Saldo actual de puntos del cliente que llama.</summary>
    Task<LoyaltyBalanceDto> BalanceAsync(Caller caller, CancellationToken ct);
}
