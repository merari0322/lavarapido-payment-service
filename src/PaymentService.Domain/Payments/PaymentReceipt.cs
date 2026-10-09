using PaymentService.Domain.Common;

namespace PaymentService.Domain.Payments;

/// <summary>
/// Comprobante que respalda un pago. Es parte del aggregate
/// Payment: solo se crea y se revisa a través de él, por eso sus métodos son internal.
///
/// Es una entidad aparte del pago (y no un dato del pago) para que un comprobante corregido no
/// borre el anterior: ambos quedan en el historial.
/// </summary>
public sealed class PaymentReceipt : Entity<long>
{
    /// <summary>
    /// Todo comprobante tiene un archivo. Un pago registrado en persona por el admin no tiene
    /// imagen, así que su "archivo" es este marcador. Como no es imagen (ImageSource), la web no
    /// intenta mostrarlo.
    /// </summary>
    public const string InPersonFileMarker = "manual:registrado-por-admin";

    private const int MaxReferenceLength = 100;
    private const int MaxReviewCommentLength = 300;

    private PaymentReceipt() { }

    public string FileUrl { get; private set; } = string.Empty;

    /// <summary>Número de referencia impreso en el comprobante: es el objetivo del control antifraude.</summary>
    public string? TransactionReference { get; private set; }

    /// <summary>Lo que el comprobante dice que se pagó; se compara con Payment.Amount.</summary>
    public decimal? ReportedAmount { get; private set; }

    public long UploadedBy { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    // Quién revisó y cuándo van siempre juntos: por eso solo se asignan en MarkReviewed.
    public long? ReviewedBy { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewComment { get; private set; }

    /// <summary>true si FileUrl es una imagen que se puede mostrar (no el marcador de pago en persona).</summary>
    public bool HasImage => ImageSource.IsImage(FileUrl);

    internal static PaymentReceipt Create(string fileUrl, long uploadedBy, string? transactionReference,
        decimal? reportedAmount, DateTime uploadedAtUtc)
    {
        Guard.Against(string.IsNullOrWhiteSpace(fileUrl), DomainErrorCodes.InvalidReceiptFile,
            "El comprobante debe tener un archivo.");
        Guard.PositiveId(uploadedBy, DomainErrorCodes.InvalidReceiptUploader, "El comprobante debe indicar quién lo subió.");
        Guard.Against(reportedAmount is <= 0, DomainErrorCodes.InvalidReportedAmount,
            "El monto reportado en el comprobante debe ser mayor que cero.");

        return new PaymentReceipt
        {
            FileUrl = fileUrl,
            UploadedBy = uploadedBy,
            TransactionReference = Guard.Optional(transactionReference, MaxReferenceLength,
                DomainErrorCodes.InvalidTransactionReference,
                $"La referencia no puede superar {MaxReferenceLength} caracteres."),
            ReportedAmount = reportedAmount,
            UploadedAtUtc = uploadedAtUtc
        };
    }

    /// <summary>Deja constancia de quién revisó el comprobante, cuándo y con qué comentario.</summary>
    internal void MarkReviewed(long reviewedBy, string? comment, DateTime reviewedAtUtc)
    {
        Guard.PositiveId(reviewedBy, DomainErrorCodes.InvalidReviewer, "La revisión debe indicar quién revisa.");

        ReviewedBy = reviewedBy;
        ReviewedAtUtc = reviewedAtUtc;
        ReviewComment = Guard.Optional(comment, MaxReviewCommentLength, DomainErrorCodes.InvalidReviewComment,
            $"El comentario de revisión no puede superar {MaxReviewCommentLength} caracteres.");
    }
}
