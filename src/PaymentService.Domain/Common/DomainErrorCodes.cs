namespace PaymentService.Domain.Common;

/// <summary>
/// Códigos estables de las reglas de negocio que rompe un DomainException. La web los traduce con
/// API_ERRORS.&lt;code&gt;, así que son contrato público: no se renombran. Están juntos (igual que
/// ErrorCodes en Application) para no repetir el mismo texto en varios aggregates.
/// </summary>
public static class DomainErrorCodes
{
    // ---- Pagos
    public const string InvalidPaymentBooking = "INVALID_PAYMENT_BOOKING";
    public const string InvalidPaymentAccount = "INVALID_PAYMENT_ACCOUNT";
    public const string InvalidPaymentAmount = "INVALID_PAYMENT_AMOUNT";
    public const string InvalidPaymentStatus = "INVALID_PAYMENT_STATUS";
    public const string InvalidPaymentStatusTransition = "INVALID_PAYMENT_STATUS_TRANSITION";
    public const string PaymentReceiptRequired = "PAYMENT_RECEIPT_REQUIRED";
    public const string InvalidReviewer = "INVALID_REVIEWER";
    public const string InvalidRejectionReason = "INVALID_REJECTION_REASON";

    // ---- Comprobantes
    public const string InvalidReceiptFile = "INVALID_RECEIPT_FILE";
    public const string InvalidReceiptUploader = "INVALID_RECEIPT_UPLOADER";
    public const string InvalidReportedAmount = "INVALID_REPORTED_AMOUNT";
    public const string InvalidTransactionReference = "INVALID_TRANSACTION_REFERENCE";
    public const string InvalidReviewComment = "INVALID_REVIEW_COMMENT";

    // ---- Cuentas del lavadero
    public const string InvalidQrImage = "INVALID_QR_IMAGE";
    public const string InvalidAccountHolder = "INVALID_ACCOUNT_HOLDER";
    public const string InvalidAccountNumber = "INVALID_ACCOUNT_NUMBER";
    public const string InvalidAccountInstructions = "INVALID_ACCOUNT_INSTRUCTIONS";

    // ---- Fidelización
    public const string InvalidLoyaltyCustomer = "INVALID_LOYALTY_CUSTOMER";
    public const string InvalidLoyaltyBooking = "INVALID_LOYALTY_BOOKING";
    public const string InvalidLoyaltyPoints = "INVALID_LOYALTY_POINTS";
    public const string InvalidLoyaltyBalance = "INVALID_LOYALTY_BALANCE";
    public const string InvalidLoyaltyDescription = "INVALID_LOYALTY_DESCRIPTION";
    public const string LoyaltyInsufficientBalance = "LOYALTY_INSUFFICIENT_BALANCE";

    // ---- Promociones
    public const string InvalidDiscountType = "INVALID_DISCOUNT_TYPE";
    public const string PromotionDiscountUnsupported = "PROMOTION_DISCOUNT_UNSUPPORTED";
    public const string InvalidPromotionDiscount = "INVALID_PROMOTION_DISCOUNT";
    public const string InvalidPromotionPrice = "INVALID_PROMOTION_PRICE";
    public const string InvalidPromotionDuration = "INVALID_PROMOTION_DURATION";
    public const string InvalidPromotionRange = "INVALID_PROMOTION_RANGE";
    public const string InvalidPromotionRequiredPoints = "INVALID_PROMOTION_REQUIRED_POINTS";
    public const string InvalidPromotionBenefits = "INVALID_PROMOTION_BENEFITS";
    public const string InvalidPromotionCode = "INVALID_PROMOTION_CODE";
    public const string InvalidPromotionName = "INVALID_PROMOTION_NAME";
    public const string InvalidPromotionDescription = "INVALID_PROMOTION_DESCRIPTION";
    public const string InvalidPromotionIcon = "INVALID_PROMOTION_ICON";

    // ---- Canje de promociones
    public const string PromotionNotRedeemable = "PROMOTION_NOT_REDEEMABLE";
    public const string PromotionAlreadyRedeemed = "PROMOTION_ALREADY_REDEEMED";
    public const string PromotionMinPurchaseNotMet = "PROMOTION_MIN_PURCHASE_NOT_MET";
    public const string PromotionExhausted = "PROMOTION_EXHAUSTED";
    public const string PromotionCustomerLimitReached = "PROMOTION_CUSTOMER_LIMIT_REACHED";
    public const string InvalidRedemptionBooking = "INVALID_REDEMPTION_BOOKING";
    public const string PromotionNothingToDiscount = "PROMOTION_NOTHING_TO_DISCOUNT";
}
