using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Api.Http;
using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Adaptador HTTP de entrada para el cliente: ver las cuentas (con su QR), reportar un pago y
/// consultar los suyos. Solo traduce HTTP ↔ casos de uso; ninguna regla de negocio vive aquí.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentCommandUseCases _commands;
    private readonly IPaymentQueryUseCases _queries;
    private readonly IPaymentAccountUseCases _accounts;

    public PaymentsController(IPaymentCommandUseCases commands, IPaymentQueryUseCases queries,
        IPaymentAccountUseCases accounts)
    {
        _commands = commands;
        _queries = queries;
        _accounts = accounts;
    }

    /// <summary>Cuentas activas del lavadero que reciben comprobante (Nequi, Daviplata, transferencia).</summary>
    [HttpGet("payment-accounts")]
    public Task<IReadOnlyList<PaymentAccountDto>> Accounts(CancellationToken ct) =>
        _accounts.ListForCustomersAsync(ct);

    /// <summary>Reporta un pago con su comprobante; queda en revisión.</summary>
    [HttpPost("payments")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<PaymentDto>> Report(ReportPaymentRequest request, CancellationToken ct)
    {
        var command = new ReportPaymentCommand(request.BookingId, request.PaymentAccountId,
            request.TransactionReference, request.ReceiptImage, request.ReportedAmount);
        var payment = await _commands.ReportAsync(command, HttpContext.GetCaller(), ct);
        return Created($"/api/v1/payments/{payment.Id}", payment);
    }

    /// <summary>Pagos de las reservas del cliente que llama.</summary>
    [HttpGet("payments/me")]
    [Authorize(Roles = "CLIENT")]
    public Task<IReadOnlyList<PaymentDto>> Mine(CancellationToken ct) => _queries.MineAsync(ct);

    /// <summary>
    /// Cada reserva del cliente con el estado de su último pago y si todavía se puede pagar: el
    /// historial muestra "Pagar" con este dato, sin repetir la regla en la web ni en la app.
    /// </summary>
    [HttpGet("payments/me/bookings")]
    [Authorize(Roles = "CLIENT")]
    public Task<IReadOnlyList<BookingPaymentStateDto>> MyBookings(CancellationToken ct) => _queries.MyBookingsAsync(ct);

    /// <summary>Un pago propio (404 si no existe o es de otra persona).</summary>
    [HttpGet("payments/{id:long}")]
    [Authorize(Roles = "CLIENT")]
    public Task<PaymentDto> GetMine(long id, CancellationToken ct) => _queries.GetMineAsync(id, ct);
}
