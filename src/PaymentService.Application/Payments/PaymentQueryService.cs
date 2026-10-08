using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Consultas de pagos. La visibilidad la decide booking-service con el token del que llama: el
/// cliente solo recibe sus reservas, así que solo ve los pagos de esas reservas.
/// </summary>
public sealed class PaymentQueryService : IPaymentQueries
{
    // Tope de la cola del admin: los más recientes primero.
    private const int AdminListLimit = 200;

    private readonly IPaymentRepository _payments;
    private readonly IBookingDirectory _bookings;
    private readonly PaymentViewFactory _views;

    public PaymentQueryService(IPaymentRepository payments, IBookingDirectory bookings, PaymentViewFactory views)
    {
        _payments = payments;
        _bookings = bookings;
        _views = views;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentView>> MineAsync(Caller caller, CancellationToken ct)
    {
        var bookings = (await _bookings.MineAsync(caller.BearerToken, ct)).ToDictionary(b => b.Id);
        if (bookings.Count == 0) return Array.Empty<PaymentView>();

        var payments = await _payments.ListForBookingsAsync(bookings.Keys, ct);
        return await _views.CreateManyAsync(payments, bookings, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentView> GetMineAsync(long paymentId, Caller caller, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        // Si la reserva no es suya, booking-service no la devuelve: se responde 404 igual que si
        // el pago no existiera, para no revelar pagos ajenos.
        var booking = await _bookings.GetForCustomerAsync(payment.BookingId, caller.BearerToken, ct)
            ?? throw new NotFoundException(ErrorCodes.PaymentNotFound, $"No existe el pago {paymentId}.");
        return await _views.CreateAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentView>> ListAsync(string? status, Caller admin, CancellationToken ct)
    {
        PaymentStatus? filter = string.IsNullOrWhiteSpace(status) ? null : PaymentStatusCodes.Parse(status);
        var payments = await _payments.ListAsync(filter, AdminListLimit, ct);

        // Las reservas se piden en paralelo (son llamadas HTTP independientes a booking-service).
        var lookups = payments.Select(p => p.BookingId).Distinct()
            .Select(id => _bookings.GetForAdminAsync(id, admin.BearerToken, ct));
        var bookings = (await Task.WhenAll(lookups)).OfType<BookingInfo>().ToDictionary(b => b.Id);

        return await _views.CreateManyAsync(payments, bookings, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentView> GetAsync(long paymentId, Caller admin, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        var booking = await _bookings.GetForAdminAsync(payment.BookingId, admin.BearerToken, ct);
        return await _views.CreateAsync(payment, booking, ct);
    }
}
