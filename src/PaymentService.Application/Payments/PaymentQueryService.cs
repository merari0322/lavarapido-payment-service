using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Consultas de pagos. La visibilidad la decide booking-service con la identidad del request: el
/// cliente solo recibe sus reservas, así que solo ve los pagos de esas reservas.
/// </summary>
public sealed class PaymentQueryService : IPaymentQueryUseCases
{
    // Tope de la cola del admin: los más recientes primero.
    private const int AdminListLimit = 200;

    private readonly IPaymentRepository _payments;
    private readonly IBookingDirectory _bookings;
    private readonly PaymentDtoAssembler _dtos;

    public PaymentQueryService(IPaymentRepository payments, IBookingDirectory bookings, PaymentDtoAssembler dtos)
    {
        _payments = payments;
        _bookings = bookings;
        _dtos = dtos;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentDto>> MineAsync(CancellationToken ct)
    {
        var bookings = (await _bookings.MineAsync(ct)).ToDictionary(b => b.Id);
        if (bookings.Count == 0) return Array.Empty<PaymentDto>();

        var payments = await _payments.ListForBookingsAsync(bookings.Keys, ct);
        return await _dtos.ToDtosAsync(payments, bookings, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentDto> GetMineAsync(long paymentId, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        // Si la reserva no es suya, booking-service no la devuelve: se responde 404 igual que si
        // el pago no existiera, para no revelar pagos ajenos.
        var booking = await _bookings.GetForCustomerAsync(payment.BookingId, ct)
            ?? throw new NotFoundException(ErrorCodes.PaymentNotFound, $"No existe el pago {paymentId}.");
        return await _dtos.ToDtoAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentDto>> ListAsync(string? status, CancellationToken ct)
    {
        PaymentStatus? filter = string.IsNullOrWhiteSpace(status) ? null : PaymentStatusCodes.Parse(status);
        var payments = await _payments.ListAsync(filter, AdminListLimit, ct);

        // Las reservas se piden en paralelo (son llamadas HTTP independientes a booking-service).
        var lookups = payments.Select(p => p.BookingId).Distinct()
            .Select(id => _bookings.GetForAdminAsync(id, ct));
        var bookings = (await Task.WhenAll(lookups)).OfType<BookingInfo>().ToDictionary(b => b.Id);

        return await _dtos.ToDtosAsync(payments, bookings, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentDto> GetAsync(long paymentId, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        var booking = await _bookings.GetForAdminAsync(payment.BookingId, ct);
        return await _dtos.ToDtoAsync(payment, booking, ct);
    }
}
