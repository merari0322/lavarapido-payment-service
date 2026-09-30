using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.Payments;
public sealed record PaymentDto(
    long Id,
    long BookingId,
    short PaymentAccountId,
    decimal Amount,
    string Status,
    DateTime? ProcessedAtUtc,
    long? ApprovedBy,
    string? RejectionReason,
    IReadOnlyList<ReceiptDto> Receipts);

public sealed record ReceiptDto(
    long Id,
    string FileUrl,
    string? TransactionReference,
    decimal? ReportedAmount,
    long UploadedBy,
    DateTime UploadedAtUtc);
