using PaymentService.Domain.Common;

namespace PaymentService.Domain.Payments;

/// <summary>
/// Los cinco estados de payment.payment_status. Los valores numéricos SON los IDs sembrados por la
/// migración 015 (una fila por INSERT, en este orden). APPROVED = 3 está además fijado en el índice
/// único filtrado ux_payment_one_approved_per_booking, y la migración se detiene si no coincide.
/// </summary>
public enum PaymentStatus : short
{
    Pending = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Refunded = 5
}

/// <summary>
/// Traducción entre el enum y el código de la tabla (PENDING, IN_REVIEW...), que es lo que viaja
/// en el JSON y en los filtros. Vive en el dominio porque esos códigos son lenguaje del negocio.
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
        throw new DomainException("INVALID_PAYMENT_STATUS", $"Estado de pago desconocido: {code}.");
    }
}
