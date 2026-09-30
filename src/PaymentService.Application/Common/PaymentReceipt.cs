using PaymentService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Application.Common;
public sealed class PaymentReceipt : Entity<long>
{
    private PaymentReceipt() { }

    public string FileUrl { get; private set; } = string.Empty;
    public string? TransactionReference { get; private set; }
    public decimal? ReportedAmount { get; private set; }
    public long UploadedBy { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    internal static PaymentReceipt Create(
        string fileUrl, long uploadedBy, string? transactionReference, decimal? reportedAmount)
    {
        return new PaymentReceipt
        {
            FileUrl = fileUrl,
            UploadedBy = uploadedBy,
            TransactionReference = transactionReference,
            ReportedAmount = reportedAmount,
            UploadedAtUtc = DateTime.UtcNow
        };
    }
}