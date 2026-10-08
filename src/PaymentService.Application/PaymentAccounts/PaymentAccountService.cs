using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.PaymentAccounts;

namespace PaymentService.Application.PaymentAccounts;

/// <summary>Casos de uso de las cuentas del lavadero (consulta pública y gestión del admin).</summary>
public sealed class PaymentAccountService : IPaymentAccountUseCases
{
    private readonly IPaymentAccountRepository _accounts;
    private readonly PaymentAccountReader _reader;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentAccountService(IPaymentAccountRepository accounts, PaymentAccountReader reader, IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _reader = reader;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PaymentAccountDto>> ListForCustomersAsync(CancellationToken ct) =>
        _reader.ListAsync(onlyActive: true, onlyReceiptMethods: true, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<PaymentAccountDto>> ListAllAsync(CancellationToken ct) =>
        _reader.ListAsync(onlyActive: false, onlyReceiptMethods: false, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentMethodDto>> ListMethodsAsync(CancellationToken ct) =>
        (await _accounts.ListMethodTypesAsync(ct))
            .Select(m => new PaymentMethodDto(m.Id, m.Code, m.Name, m.RequiresReceipt))
            .ToList();

    /// <inheritdoc />
    public async Task<PaymentAccountDto> CreateAsync(SaveAccountCommand command, CancellationToken ct)
    {
        var method = await RequireMethodAsync(command.MethodCode, ct);
        var account = PaymentAccount.Create(method, command.AccountHolder, command.AccountNumber,
            command.QrImageUrl, command.Instructions, command.Active);

        _accounts.Add(account);
        await _unitOfWork.CommitAsync(ct);
        return await _reader.ToDtoAsync(account, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentAccountDto> UpdateAsync(short id, SaveAccountCommand command, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(ErrorCodes.PaymentAccountNotFound, $"No existe la cuenta {id}.");
        var method = await RequireMethodAsync(command.MethodCode, ct);
        account.Update(method, command.AccountHolder, command.AccountNumber, command.QrImageUrl,
            command.Instructions, command.Active);

        await _unitOfWork.CommitAsync(ct);
        return await _reader.ToDtoAsync(account, ct);
    }

    private async Task<PaymentMethodType> RequireMethodAsync(string? code, CancellationToken ct) =>
        (string.IsNullOrWhiteSpace(code) ? null : await _accounts.GetMethodTypeByCodeAsync(code, ct))
        ?? throw new InvalidRequestException(ErrorCodes.InvalidPaymentMethod, $"Medio de pago desconocido: {code}.");
}
