using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Loyalty;
using PaymentService.Application.Ports.In;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Casos de uso que cambian el estado de un pago. Cada método sigue el mismo guion:
///   1. leer lo necesario (reserva en booking-service, pago, cuenta),
///   2. validar precondiciones externas (BookingPaymentGuard),
///   3. delegar la regla al aggregate (Payment.ReportWithReceipt, Approve, Reject...),
///   4. confirmar todo junto con la Unit of Work,
///   5. publicar los eventos de integración (solo después de confirmar, para no anunciar algo que
///      no quedó guardado).
/// La reserva se consulta ANTES de confirmar: si booking-service no responde, no queda un pago
/// aprobado a medias sin sus puntos.
/// </summary>
public sealed class PaymentCommandService : IPaymentCommands
{
    private readonly IPaymentRepository _payments;
    private readonly IBookingDirectory _bookings;
    private readonly BookingPaymentGuard _guard;
    private readonly AmountDueCalculator _amountDue;
    private readonly LoyaltyRewardService _rewards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _events;
    private readonly PaymentViewFactory _views;

    public PaymentCommandService(IPaymentRepository payments, IBookingDirectory bookings, BookingPaymentGuard guard,
        AmountDueCalculator amountDue, LoyaltyRewardService rewards, IUnitOfWork unitOfWork,
        IIntegrationEventPublisher events, PaymentViewFactory views)
    {
        _payments = payments;
        _bookings = bookings;
        _guard = guard;
        _amountDue = amountDue;
        _rewards = rewards;
        _unitOfWork = unitOfWork;
        _events = events;
        _views = views;
    }

    /// <inheritdoc />
    public async Task<PaymentView> ReportAsync(ReportPaymentCommand command, Caller caller, CancellationToken ct)
    {
        var booking = await _bookings.RequireForCustomerAsync(command.BookingId, caller, ct);
        await _guard.EnsureBookingIsUnpaidAsync(booking, allowOpenPayment: false, ct);
        var account = await _guard.GetUsableAccountAsync(command.PaymentAccountId, forCustomerReport: true, ct);
        await _guard.EnsureReferenceNotReusedAsync(command.TransactionReference, booking.Id, ct);

        // El monto lo calcula el servidor (total de la reserva menos cupones), nunca el navegador.
        var payment = Payment.ReportWithReceipt(booking.Id, account.Id, await _amountDue.ForAsync(booking, ct),
            command.ReceiptImage, caller.UserId, command.TransactionReference);

        _payments.Add(payment);
        await _unitOfWork.CommitAsync(ct);
        return await _views.CreateAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentView> RegisterInPersonAsync(RegisterInPersonPaymentCommand command, Caller admin,
        CancellationToken ct)
    {
        var booking = await _bookings.RequireForAdminAsync(command.BookingId, admin, ct);
        // Un pago en revisión no impide registrar el que se recibió en persona: el que llegue
        // segundo a aprobarse chocará con "ya tiene un pago aprobado".
        await _guard.EnsureBookingIsUnpaidAsync(booking, allowOpenPayment: true, ct);
        var account = await _guard.GetUsableAccountAsync(command.PaymentAccountId, forCustomerReport: false, ct);
        await _guard.EnsureReferenceNotReusedAsync(command.TransactionReference, booking.Id, ct);

        var payment = Payment.RegisterInPerson(booking.Id, account.Id, await _amountDue.ForAsync(booking, ct),
            admin.UserId, command.TransactionReference);

        _payments.Add(payment);
        await _rewards.CreditForBookingAsync(booking, admin.UserId, ct);
        await _unitOfWork.CommitAsync(ct);
        await PublishEventsAsync(payment, booking.OwnerUserId, ct);
        return await _views.CreateAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public async Task<PaymentView> ApproveAsync(long paymentId, Caller admin, CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        payment.Approve(admin.UserId);

        // El índice único filtrado también lo impide, pero así el error es claro y temprano.
        if (await _payments.HasApprovedPaymentAsync(payment.BookingId, ct))
            throw new ConflictException(ErrorCodes.PaymentAlreadyApproved, "La reserva ya tiene un pago aprobado.");

        var booking = await _bookings.GetForAdminAsync(payment.BookingId, admin.BearerToken, ct);
        if (booking is not null)
            await _rewards.CreditForBookingAsync(booking, admin.UserId, ct);

        await _unitOfWork.CommitAsync(ct);
        await PublishEventsAsync(payment, booking?.OwnerUserId, ct);
        return await _views.CreateAsync(payment, booking, ct);
    }

    /// <inheritdoc />
    public Task<PaymentView> RejectAsync(long paymentId, string reason, Caller admin, CancellationToken ct) =>
        ReviewAsync(paymentId, admin, p => p.Reject(admin.UserId, reason), ct);

    /// <inheritdoc />
    public Task<PaymentView> RefundAsync(long paymentId, Caller admin, CancellationToken ct) =>
        ReviewAsync(paymentId, admin, p => p.Refund(), ct);

    /// <summary>
    /// Guion común de las transiciones que no tocan otros aggregates: cargar, aplicar la regla del
    /// dominio, confirmar y avisar al cliente dueño de la reserva.
    /// </summary>
    private async Task<PaymentView> ReviewAsync(long paymentId, Caller admin, Action<Payment> transition,
        CancellationToken ct)
    {
        var payment = await _payments.GetRequiredAsync(paymentId, ct);
        transition(payment);

        var booking = await _bookings.GetForAdminAsync(payment.BookingId, admin.BearerToken, ct);
        await _unitOfWork.CommitAsync(ct);
        await PublishEventsAsync(payment, booking?.OwnerUserId, ct);
        return await _views.CreateAsync(payment, booking, ct);
    }

    private async Task PublishEventsAsync(Payment payment, long? customerUserId, CancellationToken ct)
    {
        foreach (var integrationEvent in PaymentIntegrationEvents.From(payment, customerUserId))
            await _events.PublishAsync(integrationEvent, ct);
        payment.ClearDomainEvents();
    }
}
