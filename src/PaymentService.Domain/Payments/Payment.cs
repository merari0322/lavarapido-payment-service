using PaymentService.Domain.Common;

namespace PaymentService.Domain.Payments;

/// <summary>
/// Aggregate root: un intento de pago de una reserva (tabla payment.payment) con sus comprobantes.
///
/// Máquina de estados que protege:
///   PENDING ──(comprobante)──► IN_REVIEW ──► APPROVED ──► REFUNDED
///                                   └──────► REJECTED
/// Una reserva puede tener varios intentos (uno rechazado y luego otro aprobado son dos filas);
/// que haya a lo sumo un APPROVED por reserva lo garantiza el índice único filtrado de la tabla y
/// la validación previa en la capa de aplicación.
///
/// El pago no guarda el medio de pago: se llega a él por la cuenta (PaymentAccountId), así un pago
/// no puede decir "Nequi" y apuntar a la cuenta de Daviplata (decisión del modelo de datos).
/// </summary>
public sealed class Payment : AggregateRoot<long>
{
    public const int MaxRejectionReasonLength = 200;

    private readonly List<PaymentReceipt> _receipts = new();

    private Payment() { }

    /// <summary>Reserva que se paga (booking-service; sin FK, contrato entre servicios).</summary>
    public long BookingId { get; private set; }

    public short PaymentAccountId { get; private set; }

    /// <summary>Monto esperado: el total de la reserva menos los cupones canjeados, nunca lo que mande el navegador.</summary>
    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>Cuándo se revisó (aprobó o rechazó), en UTC.</summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    public long? ApprovedBy { get; private set; }
    public string? RejectionReason { get; private set; }

    public IReadOnlyCollection<PaymentReceipt> Receipts => _receipts.AsReadOnly();

    /// <summary>El último comprobante subido: es el que se revisa y el que se muestra.</summary>
    public PaymentReceipt? LatestReceipt =>
        _receipts.OrderByDescending(r => r.UploadedAtUtc).ThenByDescending(r => r.Id).FirstOrDefault();

    // ------------------------------------------------------------------ creación (Factory Method)

    /// <summary>
    /// Crea el pago en PENDING validando sus datos mínimos. Es la base de los dos factory methods de
    /// abajo, que expresan los dos caminos reales por los que entra un pago.
    /// </summary>
    public static Payment Create(long bookingId, short paymentAccountId, decimal amount)
    {
        Guard.PositiveId(bookingId, "INVALID_PAYMENT_BOOKING", "El pago debe estar asociado a una reserva válida.");
        Guard.PositiveId(paymentAccountId, "INVALID_PAYMENT_ACCOUNT", "El pago debe indicar la cuenta de pago.");
        Guard.Against(amount <= 0, "INVALID_PAYMENT_AMOUNT", "El monto del pago debe ser mayor que cero.");

        return new Payment
        {
            BookingId = bookingId,
            PaymentAccountId = paymentAccountId,
            Amount = amount,
            Status = PaymentStatus.Pending
        };
    }

    /// <summary>
    /// Factory Method — el cliente pagó desde su banco (QR) y reporta el pago con el comprobante:
    /// queda en IN_REVIEW esperando que el admin lo apruebe o rechace.
    /// </summary>
    public static Payment ReportWithReceipt(long bookingId, short paymentAccountId, decimal amount,
        string receiptFile, long reportedBy, string? transactionReference)
    {
        // El cliente siempre respalda su pago con la imagen del comprobante.
        Guard.Against(!ImageSource.IsImage(receiptFile), "INVALID_RECEIPT_FILE", "El comprobante debe ser una imagen.");
        var payment = Create(bookingId, paymentAccountId, amount);
        payment.AttachReceipt(receiptFile, reportedBy, transactionReference, amount);
        payment.SubmitForReview();
        return payment;
    }

    /// <summary>
    /// Factory Method — el admin registra un pago recibido en el lavadero (efectivo o transferencia
    /// ya verificada): el soporte es su propio registro y el pago queda aprobado de una vez.
    /// </summary>
    public static Payment RegisterInPerson(long bookingId, short paymentAccountId, decimal amount,
        long registeredBy, string? transactionReference)
    {
        var payment = Create(bookingId, paymentAccountId, amount);
        payment.AttachReceipt(PaymentReceipt.InPersonFileMarker, registeredBy, transactionReference, amount);
        payment.SubmitForReview();
        payment.Approve(registeredBy);
        return payment;
    }

    // ------------------------------------------------------------------ transiciones

    /// <summary>Adjunta un comprobante; solo mientras el pago está pendiente.</summary>
    public void AttachReceipt(string fileUrl, long uploadedBy, string? transactionReference = null,
        decimal? reportedAmount = null)
    {
        EnsureStatus(PaymentStatus.Pending, "Solo se puede adjuntar un comprobante a un pago pendiente.");
        _receipts.Add(PaymentReceipt.Create(fileUrl, uploadedBy, transactionReference, reportedAmount, DateTime.UtcNow));
    }

    /// <summary>Envía el pago a revisión; exige al menos un comprobante.</summary>
    public void SubmitForReview()
    {
        EnsureStatus(PaymentStatus.Pending, "Solo un pago pendiente puede enviarse a revisión.");
        Guard.Against(_receipts.Count == 0, "PAYMENT_RECEIPT_REQUIRED",
            "Para enviar a revisión se necesita al menos un comprobante.");

        Status = PaymentStatus.InReview;
    }

    /// <summary>Aprueba el pago y marca su comprobante como revisado por quien aprueba.</summary>
    public void Approve(long approvedBy)
    {
        EnsureStatus(PaymentStatus.InReview, "Solo un pago en revisión puede aprobarse.");
        Guard.PositiveId(approvedBy, "INVALID_REVIEWER", "La aprobación debe indicar quién aprueba.");

        var now = DateTime.UtcNow;
        Status = PaymentStatus.Approved;
        ApprovedBy = approvedBy;
        ProcessedAtUtc = now;
        LatestReceipt?.MarkReviewed(approvedBy, null, now);
        AddDomainEvent(new PaymentApproved(BookingId, Amount));
    }

    /// <summary>Rechaza el pago con un motivo obligatorio, que queda también como comentario del comprobante.</summary>
    public void Reject(long rejectedBy, string reason)
    {
        EnsureStatus(PaymentStatus.InReview, "Solo un pago en revisión puede rechazarse.");
        Guard.PositiveId(rejectedBy, "INVALID_REVIEWER", "El rechazo debe indicar quién rechaza.");
        var cleanReason = Guard.Required(reason, MaxRejectionReasonLength, "INVALID_REJECTION_REASON",
            $"Para rechazar un pago hay que indicar el motivo (máximo {MaxRejectionReasonLength} caracteres).");

        var now = DateTime.UtcNow;
        Status = PaymentStatus.Rejected;
        RejectionReason = cleanReason;
        ProcessedAtUtc = now;
        LatestReceipt?.MarkReviewed(rejectedBy, cleanReason, now);
        AddDomainEvent(new PaymentRejected(BookingId, Amount, cleanReason));
    }

    /// <summary>Devuelve un pago aprobado (REFUNDED es un estado final).</summary>
    public void Refund()
    {
        EnsureStatus(PaymentStatus.Approved, "Solo un pago aprobado puede reembolsarse.");

        Status = PaymentStatus.Refunded;
        AddDomainEvent(new PaymentRefunded(BookingId, Amount));
    }

    private void EnsureStatus(PaymentStatus expected, string message) =>
        Guard.Against(Status != expected, "INVALID_PAYMENT_STATUS_TRANSITION", message);
}
