using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Ports.In;

namespace PaymentService.Api.Controllers;

/// <summary>Adaptador HTTP de entrada para las cuentas del lavadero y el catálogo de medios. Solo ADMIN.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/v1/admin")]
public sealed class AdminPaymentAccountsController : ControllerBase
{
    private readonly IPaymentAccountUseCases _accounts;

    public AdminPaymentAccountsController(IPaymentAccountUseCases accounts)
    {
        _accounts = accounts;
    }

    /// <summary>Todas las cuentas, activas o no.</summary>
    [HttpGet("payment-accounts")]
    public Task<IReadOnlyList<PaymentAccountDto>> Accounts(CancellationToken ct) => _accounts.ListAllAsync(ct);

    [HttpGet("payment-methods")]
    public Task<IReadOnlyList<PaymentMethodDto>> Methods(CancellationToken ct) => _accounts.ListMethodsAsync(ct);

    [HttpPost("payment-accounts")]
    public async Task<ActionResult<PaymentAccountDto>> Create(SaveAccountRequest request, CancellationToken ct)
    {
        var account = await _accounts.CreateAsync(ToCommand(request), ct);
        return Created($"/api/v1/admin/payment-accounts/{account.Id}", account);
    }

    [HttpPut("payment-accounts/{id}")]
    public Task<PaymentAccountDto> Update(short id, SaveAccountRequest request, CancellationToken ct) =>
        _accounts.UpdateAsync(id, ToCommand(request), ct);

    private static SaveAccountCommand ToCommand(SaveAccountRequest r) =>
        new(r.MethodCode, r.AccountHolder, r.AccountNumber, r.QrImageUrl, r.Instructions, r.Active);
}
