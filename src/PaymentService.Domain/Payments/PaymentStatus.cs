using PaymentService.Domain.Common;

namespace PaymentService.Domain.Payments;

/// <summary>
/// Estados del ciclo de vida de un pago. El dominio no conoce los IDs de la tabla
/// payment.payment_status: la traducción enum ↔ ID vive en la infraestructura (PaymentStatusIds).
/// </summary>
public enum PaymentStatus
{
    Pending,
    InReview,
    Approved,
    Rejected,
    Refunded
}

/// <summary>
/// Traducción entre el enum y su código (PENDING, IN_REVIEW...), que es lo que viaja en el JSON y
/// en los filtros. Vive en el dominio porque esos códigos son lenguaje del negocio.
/// </summary>
public static class PaymentStatusCodes
{
    private static readonly IReadOnlyDictionary<PaymentStatus, string> Codes = new Dictionary<PaymentStatus, string>
    {
        [PaymentStatus.Pending] = "PENDING",
        [PaymentStatus.InReview] = "IN_REVIEW",
        [PaymentStatus.Approved] = "APPROVED",
        [PaymentStatus.Rejected] = "REJECTED",
        [PaymentStatus.Refunded] = "REFUNDED"
    };

    public static string ToCode(this PaymentStatus status) => Codes[status];

    /// <summary>Un pago "abierto" todavía espera decisión: bloquea reportar otro para la misma reserva.</summary>
    public static bool IsOpen(this PaymentStatus status) =>
        status is PaymentStatus.Pending or PaymentStatus.InReview;

    /// <summary>Convierte el código recibido (sin distinguir mayúsculas) o lanza INVALID_PAYMENT_STATUS.</summary>
    public static PaymentStatus Parse(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        foreach (var (status, value) in Codes)
            if (value == normalized) return status;
        throw new DomainException(DomainErrorCodes.InvalidPaymentStatus, $"Estado de pago desconocido: {code}.");
    }
}
