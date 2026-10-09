using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.PaymentAccounts;

namespace PaymentService.Application.PaymentAccounts;

/// <summary>
/// Arma PaymentAccountDto juntando cada cuenta con su medio de pago. Lo usan tanto la gestión de
/// cuentas como las vistas de pagos, que antes repetían cada una su propio mapeo.
/// </summary>
public sealed class PaymentAccountReader
{
    private readonly IPaymentAccountRepository _accounts;

    public PaymentAccountReader(IPaymentAccountRepository accounts)
    {
        _accounts = accounts;
    }

    /// <summary>Todas las cuentas (incluidas inactivas, porque un pago viejo puede apuntar a una) por Id.</summary>
    public async Task<IReadOnlyDictionary<short, PaymentAccountDto>> ByIdAsync(CancellationToken ct) =>
        (await ListAsync(onlyActive: false, onlyReceiptMethods: false, ct)).ToDictionary(a => a.Id);

    public async Task<IReadOnlyList<PaymentAccountDto>> ListAsync(bool onlyActive, bool onlyReceiptMethods, CancellationToken ct)
    {
        var methods = await MethodsByIdAsync(ct);
        return (await _accounts.ListAsync(onlyActive, ct))
            .Select(a => ToDto(a, methods.GetValueOrDefault(a.PaymentMethodTypeId)))
            .Where(dto => !onlyReceiptMethods || dto.RequiresReceipt)
            .ToList();
    }

    public async Task<PaymentAccountDto> ToDtoAsync(PaymentAccount account, CancellationToken ct) =>
        ToDto(account, await _accounts.GetMethodTypeAsync(account.PaymentMethodTypeId, ct));

    private async Task<Dictionary<short, PaymentMethodType>> MethodsByIdAsync(CancellationToken ct) =>
        (await _accounts.ListMethodTypesAsync(ct)).ToDictionary(m => m.Id);

    // Si el medio no se encuentra (catálogo inconsistente) la cuenta se muestra igual, sin nombre de
    // medio, y se asume que exige comprobante (lo más restrictivo).
    private static PaymentAccountDto ToDto(PaymentAccount account, PaymentMethodType? method) =>
        new(account.Id, method?.Code ?? string.Empty, method?.Name ?? string.Empty, account.AccountHolder,
            account.AccountNumber, account.QrImageUrl, account.Instructions, account.IsActive,
            method?.RequiresReceipt ?? true);
}
