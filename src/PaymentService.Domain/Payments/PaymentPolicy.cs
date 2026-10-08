namespace PaymentService.Domain.Payments;

/// <summary>
/// Reglas de negocio del pago que no pertenecen a un solo aggregate porque dependen de datos de
/// otros (la reserva de booking-service, los cupones canjeados). Son funciones puras: el caso de
/// uso consulta los datos y le pregunta aquí qué dice el negocio.
/// </summary>
public static class PaymentPolicy
{
    // Estados de reserva (lenguaje de booking-service) en los que tiene sentido pagar: una
    // reserva cancelada o no asistida no se paga.
    private static readonly HashSet<string> PayableBookingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "CONFIRMED", "IN_PROGRESS", "COMPLETED" };

    /// <summary>Si una reserva en ese estado se puede pagar.</summary>
    public static bool IsBookingPayable(string bookingStatus) => PayableBookingStatuses.Contains(bookingStatus);

    /// <summary>
    /// Lo que realmente falta por pagar: el total de la reserva menos lo que ya descontaron los
    /// cupones canjeados en ella, nunca negativo. Es el monto del pago y también el subtotal sobre
    /// el que se calcula el siguiente cupón, así ambos usan exactamente la misma cifra.
    /// </summary>
    public static decimal AmountDue(decimal bookingTotal, decimal appliedDiscounts) =>
        Math.Max(bookingTotal - appliedDiscounts, 0m);
}
