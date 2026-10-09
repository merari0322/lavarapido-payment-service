using PaymentService.Domain.Common;

namespace PaymentService.Application.Common;

/// <summary>
/// Códigos de error que lanza la capa de aplicación (los de las reglas de un aggregate están en
/// DomainErrorCodes). La web los traduce con API_ERRORS.&lt;code&gt;, así que son contrato
/// público: no se renombran.
/// </summary>
public static class ErrorCodes
{
    public const string BookingNotFound = "BOOKING_NOT_FOUND";
    public const string BookingNotPayable = "BOOKING_NOT_PAYABLE";
    public const string BookingServiceUnavailable = "BOOKING_SERVICE_UNAVAILABLE";

    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string PaymentAlreadyApproved = "PAYMENT_ALREADY_APPROVED";
    public const string PaymentAlreadyReported = "PAYMENT_ALREADY_REPORTED";
    public const string TransactionReferenceReused = "TRANSACTION_REFERENCE_REUSED";

    /// <summary>Mismo código que la regla del aggregate: para la web es el mismo error (cuenta no válida).</summary>
    public const string InvalidPaymentAccount = DomainErrorCodes.InvalidPaymentAccount;
    public const string PaymentAccountNotFound = "PAYMENT_ACCOUNT_NOT_FOUND";
    public const string InvalidPaymentMethod = "INVALID_PAYMENT_METHOD";

    public const string PromotionNotFound = "PROMOTION_NOT_FOUND";
    public const string PromotionCodeTaken = "PROMOTION_CODE_TAKEN";

    public const string DataConflict = "DATA_CONFLICT";
}
