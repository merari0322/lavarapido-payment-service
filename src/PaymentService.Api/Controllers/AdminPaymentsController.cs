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
    private readonly IPaymentCommandUseCases _commands;
    private readonly IPaymentQueryUseCases _queries;

    public AdminPaymentsController(IPaymentCommandUseCases commands, IPaymentQueryUseCases queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Cola de pagos, opcionalmente filtrada por estado (PENDING, IN_REVIEW, APPROVED, REJECTED, REFUNDED).</summary>
    [HttpGet]
    public Task<IReadOnlyList<PaymentDto>> List([FromQuery] string? status, CancellationToken ct) =>
        _queries.ListAsync(status, ct);

    [HttpGet("{id:long}")]
    public Task<PaymentDto> Get(long id, CancellationToken ct) => _queries.GetAsync(id, ct);

    /// <summary>Lo que falta por pagar de una reserva (total menos cupones): el monto de un pago en caja.</summary>
    [HttpGet("amount-due")]
    public Task<AmountDueDto> AmountDue([FromQuery] long bookingId, CancellationToken ct) =>
        _queries.AmountDueAsync(bookingId, ct);

    /// <summary>Pago recibido en el lavadero: queda aprobado con lo que falta por pagar de la reserva.</summary>
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> RegisterManual(ManualPaymentRequest request, CancellationToken ct)
    {
        var command = new RegisterInPersonPaymentCommand(request.BookingId, request.PaymentAccountId, request.TransactionReference);
        var payment = await _commands.RegisterInPersonAsync(command, HttpContext.GetCaller(), ct);
        return Created($"/api/v1/admin/payments/{payment.Id}", payment);
    }

    [HttpPost("{id:long}/approve")]
    public Task<PaymentDto> Approve(long id, CancellationToken ct) =>
        _commands.ApproveAsync(id, HttpContext.GetCaller(), ct);

    [HttpPost("{id:long}/reject")]
    public Task<PaymentDto> Reject(long id, RejectPaymentRequest request, CancellationToken ct) =>
        _commands.RejectAsync(id, request.Reason, HttpContext.GetCaller(), ct);

    /// <summary>Devuelve un pago aprobado. Responde 204 (contrato previo de la web).</summary>
    [HttpPost("{id:long}/refund")]
    public async Task<IActionResult> Refund(long id, CancellationToken ct)
    {
        await _commands.RefundAsync(id, HttpContext.GetCaller(), ct);
        return NoContent();
    }
}
