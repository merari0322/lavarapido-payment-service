using PaymentService.Application.Common;
using PaymentService.Application.Loyalty;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Casos de uso que cambian el estado de un pago. Cada método sigue el mismo guion:
///   1. leer lo necesario (reserva en booking-service, pago, cuenta),
///   2. validar precondiciones externas (BookingPaymentGuard) ANTES de tocar el aggregate,
///   3. delegar la regla al aggregate (Payment.ReportWithReceipt, Approve, Reject, Refund),
///   4. mantener los puntos de la reserva alineados con el pago (LoyaltyRewardService),
///   5. confirmar todo junto con la Unit of Work,
///   6. publicar los eventos de integración (solo después de confirmar).
/// La reserva se consulta antes de confirmar: si booking-service no responde, no queda un pago
/// aprobado a medias sin sus puntos.
/// </summary>
public sealed class PaymentCommandService : IPaymentCommandUseCases
{
    private readonly IPaymentRepository _payments;
    private readonly IBookingDirectory _bookings;
    private readonly BookingPaymentGuard _guard;
    private readonly AmountDueCalculator _amountDue;
    private readonly LoyaltyRewardService _rewards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _events;
    private readonly PaymentDtoAssembler _dtos;
    private readonly TimeProvider _clock;

    public PaymentCommandService(IPaymentRepository payments, IBookingDirectory bookings, BookingPaymentGuard guard,
        AmountDueCalculator amountDue, LoyaltyRewardService rewards, IUnitOfWork unitOfWork,
        IIntegrationEventPublisher events, PaymentDtoAssembler dtos, TimeProvider clock)
    {
        _payments = payments;
        _bookings = bookings;
        _guard = guard;
        _amountDue = amountDue;
        _rewards = rewards;
        _unitOfWork = unitOfWork;
        _events = events;
        _dtos = dtos;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<PaymentDto> ReportAsync(ReportPaymentCommand command, Caller caller, CancellationToken ct)
    {
        var booking = await _bookings.RequireForCustomerAsync(command.BookingId, ct);
        await _guard.EnsureBookingIsUnpaidAsync(booking, allowOpenPayment: false, ct);
        var account = await _guard.GetUsableAccountAsync(command.PaymentAccountId, forCustomerReport: true, ct);
        await _guard.EnsureReferenceNotReusedAsync(command.TransactionReference, booking.Id, ct);

        // El monto lo calcula el servidor (total de la reserva menos cupones), nunca el navegador.
        var payment = Payment.ReportWithReceipt(booking.Id, account.Id, await _amountDue.ForAsync(booking, ct),
            command.ReceiptImage, caller.UserId, command.TransactionReference, Now, command.ReportedAmount);

        _payments.Add(payment);
        return await CompleteAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentDto> RegisterInPersonAsync(RegisterInPersonPaymentCommand command, Caller admin,
        CancellationToken ct)
    {
        var booking = await _bookings.RequireForAdminAsync(command.BookingId, ct);
        // Un pago en revisión no impide registrar el que se recibió en persona: el que llegue
        // segundo a aprobarse chocará con "ya tiene un pago aprobado".
        await _guard.EnsureBookingIsUnpaidAsync(booking, allowOpenPayment: true, ct);
        var account = await _guard.GetUsableAccountAsync(command.PaymentAccountId, forCustomerReport: false, ct);
        await _guard.EnsureReferenceNotReusedAsync(command.TransactionReference, booking.Id, ct);

        var now = Now;
        var payment = Payment.RegisterInPerson(booking.Id, account.Id, await _amountDue.ForAsync(booking, ct),
            admin.UserId, command.TransactionReference, now);

        _payments.Add(payment);
        var credit = await _rewards.CreditForBookingAsync(booking, admin.UserId, now, ct);
        var dto = await CompleteAsync(payment, booking, ct);
        await PublishPointsEarnedAsync(credit, now, ct);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PaymentDto> ApproveAsync(long paymentId, Caller admin, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        await _guard.EnsureNoApprovedPaymentAsync(payment.BookingId, ct);
        // Aprobar acredita los puntos de la reserva: sin ella no se sabe a quién ni cuántos, así
        // que si booking-service no la encuentra la aprobación no sigue (404) en vez de quedar sin puntos.
        var booking = await _bookings.RequireForAdminAsync(payment.BookingId, ct);
        // Si la reserva se canceló mientras el pago esperaba revisión, ya no se aprueba: se rechaza
        // (o se devuelve el dinero por fuera) en lugar de acreditar puntos de una reserva que no fue.
        _guard.EnsureBookingIsPayable(booking);

        var now = Now;
        payment.Approve(admin.UserId, now);
        var credit = await _rewards.CreditForBookingAsync(booking, admin.UserId, now, ct);
        var dto = await CompleteAsync(payment, booking, ct);
        await PublishPointsEarnedAsync(credit, now, ct);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PaymentDto> RejectAsync(long paymentId, string reason, Caller admin, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        payment.Reject(admin.UserId, reason, Now);

        // La reserva solo hace falta para avisar al cliente y mostrarla: si ya no existe, el
        // rechazo sigue siendo válido.
        var booking = await _bookings.GetForAdminAsync(payment.BookingId, ct);
        return await CompleteAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentDto> RefundAsync(long paymentId, Caller admin, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        var now = Now;
        payment.Refund(now);
        // Los puntos que ganó la reserva con este pago se devuelven junto con el dinero.
        await _rewards.RevokeForBookingAsync(payment.BookingId, admin.UserId, now, ct);

        var booking = await _bookings.GetForAdminAsync(payment.BookingId, ct);
        return await CompleteAsync(payment, booking, ct);
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Pasos 5 y 6 del guion: confirmar, publicar los eventos del pago y devolver su vista.</summary>
    private async Task<PaymentDto> CompleteAsync(Payment payment, BookingInfo? booking, CancellationToken ct)
    {
        await _unitOfWork.CommitAsync(ct);
        await _events.PublishAndClearAsync(payment, PaymentIntegrationEvents.From(payment, booking?.OwnerUserId), ct);
        return await _dtos.ToDtoAsync(payment, booking, ct);
    }

    /// <summary>
    /// Después de confirmar (los puntos ya quedaron en el ledger): avisa cuántos puntos ganó el
    /// cliente y qué cupones desbloqueó. Va después de payment.confirmed para que el aviso del pago
    /// llegue primero.
    /// </summary>
    private async Task PublishPointsEarnedAsync(LoyaltyCredit? credit, DateTime occurredOnUtc, CancellationToken ct)
    {
        if (credit is not null)
            await _events.PublishAsync(LoyaltyIntegrationEvents.PointsEarned(credit, occurredOnUtc), ct);
    }
}
