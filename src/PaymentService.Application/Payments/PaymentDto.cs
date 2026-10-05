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

/// <summary>Pago con los datos de su reserva y de la cuenta, para las pantallas.</summary>
public sealed record PaymentView(
    long Id,
    string Status,
    decimal Amount,
    DateTime? ProcessedAtUtc,
    string? RejectionReason,
    string? TransactionReference,
    string? ReceiptImage,
    DateTime? ReportedAtUtc,
    long? ReportedBy,
    PaymentAccountDto? Account,
    BookingInfo? Booking);

public sealed record PaymentAccountDto(
    short Id,
    string MethodCode,
    string MethodName,
    string AccountHolder,
    string? AccountNumber,
    string? QrImageUrl,
    string? Instructions,
    bool Active,
    bool RequiresReceipt);

public sealed record PaymentMethodDto(short Id, string Code, string Name, bool RequiresReceipt);

public sealed record ReportPaymentCommand(long BookingId, short PaymentAccountId, string? TransactionReference,
    string ReceiptImage);

public sealed record SaveAccountCommand(string MethodCode, string AccountHolder, string? AccountNumber,
    string? QrImageUrl, string? Instructions, bool Active);
