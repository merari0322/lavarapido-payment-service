using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Payments;

namespace PaymentService.Api.Controllers;
public sealed record RegisterPaymentRequest(long BookingId, short PaymentAccountId, decimal Amount);
public sealed record AttachReceiptRequest(
    string FileUrl, long UploadedBy, string? TransactionReference, decimal? ReportedAmount);
public sealed record ApprovePaymentRequest(long ApprovedBy);
public sealed record RejectPaymentRequest(string Reason);

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentApplicationService _service;

    public PaymentsController(PaymentApplicationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterPaymentRequest request, CancellationToken ct)
    {
        var id = await _service.RegisterPaymentAsync(
            request.BookingId, request.PaymentAccountId, request.Amount, ct);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PaymentDto>> GetById(long id, CancellationToken ct) =>
        Ok(await _service.GetAsync(id, ct));

    [HttpPost("{id:long}/receipts")]
    public async Task<IActionResult> AttachReceipt(long id, AttachReceiptRequest request, CancellationToken ct)
    {
        await _service.AttachReceiptAsync(
            id, request.FileUrl, request.UploadedBy, request.TransactionReference, request.ReportedAmount, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        await _service.SubmitForReviewAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, ApprovePaymentRequest request, CancellationToken ct)
    {
        await _service.ApproveAsync(id, request.ApprovedBy, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, RejectPaymentRequest request, CancellationToken ct)
    {
        await _service.RejectAsync(id, request.Reason, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/refund")]
    public async Task<IActionResult> Refund(long id, CancellationToken ct)
    {
        await _service.RefundAsync(id, ct);
        return NoContent();
    }
}
