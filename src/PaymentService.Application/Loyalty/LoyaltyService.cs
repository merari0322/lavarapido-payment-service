using PaymentService.Application.Common;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Persistence;

namespace PaymentService.Application.Loyalty;

/// <summary>
/// Saldo de puntos del cliente. Los puntos desbloquean promociones, no se gastan (RF-026: "al
/// llegar a ciertos porcentajes con los puntos acumulados se le van desbloqueando los beneficios o
/// promociones"); el canje de cupones vive en Promotions (CustomerPromotionService).
/// </summary>
public sealed class LoyaltyService : ILoyaltyUseCases
{
    private readonly ILoyaltyLedgerRepository _ledger;

    public LoyaltyService(ILoyaltyLedgerRepository ledger)
    {
        _ledger = ledger;
    }

    /// <inheritdoc />
    public async Task<LoyaltyBalanceDto> BalanceAsync(Caller caller, CancellationToken ct) =>
        new((await _ledger.CurrentBalanceAsync(caller.UserId, ct)).Points);
}
