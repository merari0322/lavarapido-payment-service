using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// IDs de payment.payment_status. A diferencia de los otros catálogos (CatalogIds), estos son
/// fijos y no se leen al arrancar: la migración 015 los siembra en este orden, el índice único
/// filtrado ux_payment_one_approved_per_booking tiene escrito APPROVED = 3 y la migración 019
/// detiene el despliegue si no coinciden.
/// </summary>
internal static class PaymentStatusIds
{
    private static readonly IReadOnlyDictionary<PaymentStatus, short> Ids = new Dictionary<PaymentStatus, short>
    {
        [PaymentStatus.Pending] = 1,
        [PaymentStatus.InReview] = 2,
        [PaymentStatus.Approved] = 3,
        [PaymentStatus.Rejected] = 4,
        [PaymentStatus.Refunded] = 5
    };

    private static readonly IReadOnlyDictionary<short, PaymentStatus> Statuses = Ids.ToDictionary(p => p.Value, p => p.Key);

    public static short ToId(PaymentStatus status) => Ids[status];

    public static PaymentStatus ToStatus(short id) =>
        Statuses.TryGetValue(id, out var status)
            ? status
            : throw new InvalidOperationException($"payment.payment_status id {id} is not a known status.");
}
