using PaymentService.Domain.PaymentAccounts;

namespace PaymentService.Application.Ports.Out.Persistence;

/// <summary>Repository de las cuentas del lavadero y del catálogo de medios de pago.</summary>
public interface IPaymentAccountRepository
{
    Task<IReadOnlyList<PaymentAccount>> ListAsync(bool onlyActive, CancellationToken ct);

    Task<PaymentAccount?> GetByIdAsync(short id, CancellationToken ct);

    /// <summary>Catálogo completo de medios de pago, en su orden de presentación.</summary>
    Task<IReadOnlyList<PaymentMethodType>> ListMethodTypesAsync(CancellationToken ct);

    Task<PaymentMethodType?> GetMethodTypeAsync(short id, CancellationToken ct);

    /// <summary>Busca el medio por código sin distinguir mayúsculas.</summary>
    Task<PaymentMethodType?> GetMethodTypeByCodeAsync(string code, CancellationToken ct);

    void Add(PaymentAccount account);
}
