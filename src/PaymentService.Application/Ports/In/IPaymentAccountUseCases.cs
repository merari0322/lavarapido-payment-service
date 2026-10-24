using PaymentService.Application.PaymentAccounts;

namespace PaymentService.Application.Ports.In;

/// <summary>Puerto de entrada: cuentas del lavadero (con su QR) y catálogo de medios de pago.</summary>
public interface IPaymentAccountUseCases
{
    /// <summary>Cuentas activas a las que el cliente puede pagar desde la app (las que exigen comprobante).</summary>
    Task<IReadOnlyList<PaymentAccountDto>> ListForCustomersAsync(CancellationToken ct);

    /// <summary>Todas las cuentas, activas o no, para el admin.</summary>
    Task<IReadOnlyList<PaymentAccountDto>> ListAllAsync(CancellationToken ct);

    Task<IReadOnlyList<PaymentMethodDto>> ListMethodsAsync(CancellationToken ct);

    Task<PaymentAccountDto> CreateAsync(SaveAccountCommand command, CancellationToken ct);

    Task<PaymentAccountDto> UpdateAsync(short id, SaveAccountCommand command, CancellationToken ct);
}
