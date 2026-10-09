using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Common;

/// <summary>
/// Búsquedas que, si no encuentran nada, deben terminar el caso de uso con 404. Están aquí una sola
/// vez para que todos los casos de uso usen el mismo código y el mismo mensaje.
/// </summary>
internal static class RequiredLookups
{
    public static async Task<Payment> GetRequiredAsync(this IPaymentRepository payments, long paymentId, CancellationToken ct) =>
        await payments.GetByIdAsync(paymentId, ct)
        ?? throw new NotFoundException(ErrorCodes.PaymentNotFound, $"No existe el pago {paymentId}.");

    public static async Task<BookingInfo> RequireForCustomerAsync(this IBookingDirectory bookings, long bookingId,
        CancellationToken ct) =>
        await bookings.GetForCustomerAsync(bookingId, ct) ?? throw BookingNotFound(bookingId);

    public static async Task<BookingInfo> RequireForAdminAsync(this IBookingDirectory bookings, long bookingId,
        CancellationToken ct) =>
        await bookings.GetForAdminAsync(bookingId, ct) ?? throw BookingNotFound(bookingId);

    private static NotFoundException BookingNotFound(long bookingId) =>
        new(ErrorCodes.BookingNotFound, $"No existe la reserva {bookingId}.");
}
