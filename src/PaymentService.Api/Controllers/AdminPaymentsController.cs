using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Api.Http;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;

namespace PaymentService.Api.Controllers;

/// <summary>Adaptador HTTP de entrada para la revisión de pagos. Solo ADMIN.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/v1/admin/payments")]
public sealed class AdminPaymentsController : ControllerBase
{
    private readonly IPaymentCommands _commands;
    private readonly IPaymentQueries _queries;

    public AdminPaymentsController(IPaymentCommands commands, IPaymentQueries queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Cola de pagos, opcionalmente filtrada por estado (PENDING, IN_REVIEW, APPROVED, REJECTED, REFUNDED).</summary>
    [HttpGet]
    public Task<IReadOnlyList<PaymentView>> List([FromQuery] string? status, CancellationToken ct) =>
        _queries.ListAsync(status, HttpContext.GetCaller(), ct);

    [HttpGet("{id:long}")]
    public Task<PaymentView> Get(long id, CancellationToken ct) =>
        _queries.GetAsync(id, HttpContext.GetCaller(), ct);

    /// <summary>Pago recibido en el lavadero: queda aprobado con el total de la reserva.</summary>
    [HttpPost]
    public async Task<ActionResult<PaymentView>> RegisterManual(ManualPaymentRequest request, CancellationToken ct)
    {
        var command = new RegisterInPersonPaymentCommand(request.BookingId, request.PaymentAccountId, request.TransactionReference);
        var view = await _commands.RegisterInPersonAsync(command, HttpContext.GetCaller(), ct);
        return Created($"/api/v1/admin/payments/{view.Id}", view);
    }

    [HttpPost("{id:long}/approve")]
    public Task<PaymentView> Approve(long id, CancellationToken ct) =>
        _commands.ApproveAsync(id, HttpContext.GetCaller(), ct);

    [HttpPost("{id:long}/reject")]
    public Task<PaymentView> Reject(long id, RejectPaymentRequest request, CancellationToken ct) =>
        _commands.RejectAsync(id, request.Reason, HttpContext.GetCaller(), ct);

    /// <summary>Devuelve un pago aprobado. Responde 204 (contrato previo de la web).</summary>
    [HttpPost("{id:long}/refund")]
    public async Task<IActionResult> Refund(long id, CancellationToken ct)
    {
        await _commands.RefundAsync(id, HttpContext.GetCaller(), ct);
        return NoContent();
    }
}
