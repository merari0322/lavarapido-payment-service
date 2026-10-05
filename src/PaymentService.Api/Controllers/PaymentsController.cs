using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Payments;

namespace PaymentService.Api.Controllers;

public sealed record ReportPaymentRequest(long BookingId, short PaymentAccountId, string? TransactionReference,
    string ReceiptImage);

public sealed record RejectPaymentRequest(string Reason);

public sealed record ManualPaymentRequest(long BookingId, short PaymentAccountId, string? TransactionReference);

public sealed record SaveAccountRequest(string MethodCode, string AccountHolder, string? AccountNumber,
    string? QrImageUrl, string? Instructions, bool Active);

/// <summary>Lo que hace el cliente: ver las cuentas (con su QR), reportar un pago y ver los suyos.</summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentApplicationService _service;

    public PaymentsController(PaymentApplicationService service)
    {
        _service = service;
    }

    /// <summary>Cuentas activas del lavadero que reciben comprobante (Nequi, Daviplata, transferencia).</summary>
    [HttpGet("payment-accounts")]
    public async Task<IReadOnlyList<PaymentAccountDto>> Accounts(CancellationToken ct) =>
        await _service.ActiveAccountsAsync(ct);

    [HttpPost("payments")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<PaymentView>> Report(ReportPaymentRequest request, CancellationToken ct)
    {
        var view = await _service.ReportPaymentAsync(new ReportPaymentCommand(request.BookingId,
            request.PaymentAccountId, request.TransactionReference, request.ReceiptImage), CallerOf.Request(HttpContext), ct);
        return Created($"/api/v1/payments/{view.Id}", view);
    }

    [HttpGet("payments/me")]
    [Authorize(Roles = "CLIENT")]
    public async Task<IReadOnlyList<PaymentView>> Mine(CancellationToken ct) =>
        await _service.MineAsync(CallerOf.Request(HttpContext), ct);
}

/// <summary>Revisión de pagos y cuentas del lavadero. Solo ADMIN.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/v1/admin")]
public class AdminPaymentsController : ControllerBase
{
    private readonly PaymentApplicationService _service;

    public AdminPaymentsController(PaymentApplicationService service)
    {
        _service = service;
    }

    [HttpGet("payments")]
    public async Task<IReadOnlyList<PaymentView>> List([FromQuery] string? status, CancellationToken ct) =>
        await _service.ListAsync(status, CallerOf.Request(HttpContext), ct);

    /// <summary>Pago recibido en el lavadero: queda aprobado con el total de la reserva.</summary>
    [HttpPost("payments")]
    public async Task<ActionResult<PaymentView>> RegisterManual(ManualPaymentRequest request, CancellationToken ct)
    {
        var view = await _service.RegisterManualAsync(request.BookingId, request.PaymentAccountId,
            request.TransactionReference, CallerOf.Request(HttpContext), ct);
        return Created($"/api/v1/admin/payments/{view.Id}", view);
    }

    [HttpPost("payments/{id:long}/approve")]
    public async Task<PaymentView> Approve(long id, CancellationToken ct) =>
        await _service.ApproveAsync(id, CallerOf.Request(HttpContext), ct);

    [HttpPost("payments/{id:long}/reject")]
    public async Task<PaymentView> Reject(long id, RejectPaymentRequest request, CancellationToken ct) =>
        await _service.RejectAsync(id, request.Reason, CallerOf.Request(HttpContext), ct);

    [HttpPost("payments/{id:long}/refund")]
    public async Task<IActionResult> Refund(long id, CancellationToken ct)
    {
        await _service.RefundAsync(id, ct);
        return NoContent();
    }

    [HttpGet("payment-accounts")]
    public async Task<IReadOnlyList<PaymentAccountDto>> Accounts(CancellationToken ct) =>
        await _service.AllAccountsAsync(ct);

    [HttpGet("payment-methods")]
    public async Task<IReadOnlyList<PaymentMethodDto>> Methods(CancellationToken ct) =>
        await _service.MethodTypesAsync(ct);

    [HttpPost("payment-accounts")]
    public async Task<ActionResult<PaymentAccountDto>> CreateAccount(SaveAccountRequest request, CancellationToken ct)
    {
        var account = await _service.CreateAccountAsync(ToCommand(request), ct);
        return Created($"/api/v1/admin/payment-accounts/{account.Id}", account);
    }

    [HttpPut("payment-accounts/{id}")]
    public async Task<PaymentAccountDto> UpdateAccount(short id, SaveAccountRequest request, CancellationToken ct) =>
        await _service.UpdateAccountAsync(id, ToCommand(request), ct);

    private static SaveAccountCommand ToCommand(SaveAccountRequest r) =>
        new(r.MethodCode, r.AccountHolder, r.AccountNumber, r.QrImageUrl, r.Instructions, r.Active);
}

/// <summary>Quien llama: sub del token y el token mismo, para reenviarlo a booking-service.</summary>
internal static class CallerOf
{
    public static Caller Request(HttpContext http)
    {
        var sub = http.User.FindFirst("sub")?.Value;
        if (!long.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("The token has no valid subject");
        var header = http.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : header;
        return new Caller(userId, token);
    }
}
