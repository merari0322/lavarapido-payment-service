using PaymentService.Application.Common;
using PaymentService.Domain.Common;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

public class PaymentApplicationService
{
    // estados de reserva en los que tiene sentido pagar (la cancelada o no asistida no se paga)
    private static readonly HashSet<string> PayableBookingStatuses = new() { "CONFIRMED", "IN_PROGRESS", "COMPLETED" };

    private readonly IPaymentRepository _payments;
    private readonly IPaymentAccountRepository _accounts;
    private readonly IBookingDirectory _bookings;

    public PaymentApplicationService(IPaymentRepository payments, IPaymentAccountRepository accounts,
        IBookingDirectory bookings)
    {
        _payments = payments;
        _accounts = accounts;
        _bookings = bookings;
    }

    // ------------------------------------------------------------------ cliente

    /// <summary>
    /// El cliente pagó desde su banco y reporta el pago con el comprobante. El monto es el total
    /// de la reserva que dice booking-service, no un valor que mande el navegador.
    /// </summary>
    public async Task<PaymentView> ReportPaymentAsync(ReportPaymentCommand command, Caller caller, CancellationToken ct)
    {
        var booking = await _bookings.GetForCustomerAsync(command.BookingId, caller.BearerToken, ct)
            ?? throw new NotFoundException("BOOKING_NOT_FOUND", $"No existe la reserva {command.BookingId}.");
        if (!PayableBookingStatuses.Contains(booking.Status))
            throw new DomainException("BOOKING_NOT_PAYABLE", "Esta reserva no se puede pagar en su estado actual.");
        if (await _payments.HasApprovedPaymentAsync(booking.Id, ct))
            throw new DomainException("PAYMENT_ALREADY_APPROVED", "La reserva ya tiene un pago aprobado.");
        if (await _payments.HasOpenPaymentAsync(booking.Id, ct))
            throw new DomainException("PAYMENT_ALREADY_REPORTED", "La reserva ya tiene un pago en revisión.");

        var account = await _accounts.GetByIdAsync(command.PaymentAccountId, ct);
        if (account is null || !account.IsActive)
            throw new DomainException("INVALID_PAYMENT_ACCOUNT", "La cuenta de pago no existe o no está activa.");

        var payment = Payment.Create(booking.Id, account.Id, booking.Total);
        payment.AttachReceipt(command.ReceiptImage, caller.UserId, command.TransactionReference?.Trim(), booking.Total);
        payment.SubmitForReview();

        await _payments.AddAsync(payment, ct);
        await _payments.SaveChangesAsync(ct);
        return await ViewAsync(payment, booking, ct);
    }

    /// <summary>Pagos de las reservas del cliente que llama.</summary>
    public async Task<IReadOnlyList<PaymentView>> MineAsync(Caller caller, CancellationToken ct)
    {
        var bookings = await _bookings.MineAsync(caller.BearerToken, ct);
        if (bookings.Count == 0) return Array.Empty<PaymentView>();
        var byId = bookings.ToDictionary(b => b.Id);
        var payments = await _payments.ListForBookingsAsync(byId.Keys.ToList(), ct);
        var accounts = await AccountsByIdAsync(ct);
        return payments.Select(p => View(p, byId.GetValueOrDefault(p.BookingId), accounts)).ToList();
    }

    public async Task<IReadOnlyList<PaymentAccountDto>> ActiveAccountsAsync(CancellationToken ct)
    {
        var methods = await MethodsByIdAsync(ct);
        return (await _accounts.ListAsync(onlyActive: true, ct))
            .Where(a => methods.TryGetValue(a.PaymentMethodTypeId, out var m) && m.RequiresReceipt)
            .Select(a => ToDto(a, methods))
            .ToList();
    }

    // ------------------------------------------------------------------ admin

    public async Task<IReadOnlyList<PaymentView>> ListAsync(string? status, Caller admin, CancellationToken ct)
    {
        PaymentStatus? filter = string.IsNullOrWhiteSpace(status) ? null : ParseStatus(status);
        var payments = await _payments.ListAsync(filter, 200, ct);
        var accounts = await AccountsByIdAsync(ct);
        var bookings = new Dictionary<long, BookingInfo?>();
        foreach (var bookingId in payments.Select(p => p.BookingId).Distinct())
            bookings[bookingId] = await _bookings.GetForAdminAsync(bookingId, admin.BearerToken, ct);
        return payments.Select(p => View(p, bookings.GetValueOrDefault(p.BookingId), accounts)).ToList();
    }

    /// <summary>
    /// El admin registra un pago recibido en el lavadero (efectivo o transferencia ya verificada):
    /// el monto es el total de la reserva y queda aprobado de una vez.
    /// </summary>
    public async Task<PaymentView> RegisterManualAsync(long bookingId, short paymentAccountId, string? reference,
        Caller admin, CancellationToken ct)
    {
        var booking = await _bookings.GetForAdminAsync(bookingId, admin.BearerToken, ct)
            ?? throw new NotFoundException("BOOKING_NOT_FOUND", $"No existe la reserva {bookingId}.");
        if (!PayableBookingStatuses.Contains(booking.Status))
            throw new DomainException("BOOKING_NOT_PAYABLE", "Esta reserva no se puede pagar en su estado actual.");
        if (await _payments.HasApprovedPaymentAsync(booking.Id, ct))
            throw new DomainException("PAYMENT_ALREADY_APPROVED", "La reserva ya tiene un pago aprobado.");

        var account = await _accounts.GetByIdAsync(paymentAccountId, ct);
        if (account is null || !account.IsActive)
            throw new DomainException("INVALID_PAYMENT_ACCOUNT", "La cuenta de pago no existe o no está activa.");

        var payment = Payment.Create(booking.Id, account.Id, booking.Total);
        // sin imagen: el soporte es el registro del admin que recibió el pago
        payment.AttachReceipt("manual:registrado-por-admin", admin.UserId, reference?.Trim(), booking.Total);
        payment.SubmitForReview();
        payment.Approve(admin.UserId);

        await _payments.AddAsync(payment, ct);
        await _payments.SaveChangesAsync(ct);
        return await ViewAsync(payment, booking, ct);
    }

    public async Task<PaymentView> ApproveAsync(long paymentId, Caller admin, CancellationToken ct)
    {
        var payment = await LoadAsync(paymentId, ct);

        // La base también lo impide (índice único), pero así el error es claro.
        if (await _payments.HasApprovedPaymentAsync(payment.BookingId, ct))
            throw new DomainException("PAYMENT_ALREADY_APPROVED", "La reserva ya tiene un pago aprobado.");

        payment.Approve(admin.UserId);
        await _payments.SaveChangesAsync(ct);
        return await ViewAsync(payment, await _bookings.GetForAdminAsync(payment.BookingId, admin.BearerToken, ct), ct);
    }

    public async Task<PaymentView> RejectAsync(long paymentId, string reason, Caller admin, CancellationToken ct)
    {
        var payment = await LoadAsync(paymentId, ct);
        payment.Reject(reason);
        await _payments.SaveChangesAsync(ct);
        return await ViewAsync(payment, await _bookings.GetForAdminAsync(payment.BookingId, admin.BearerToken, ct), ct);
    }

    public Task RefundAsync(long paymentId, CancellationToken ct) =>
        ExecuteAsync(paymentId, p => p.Refund(), ct);

    public async Task<IReadOnlyList<PaymentAccountDto>> AllAccountsAsync(CancellationToken ct)
    {
        var methods = await MethodsByIdAsync(ct);
        return (await _accounts.ListAsync(onlyActive: false, ct)).Select(a => ToDto(a, methods)).ToList();
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> MethodTypesAsync(CancellationToken ct) =>
        (await _accounts.MethodTypesAsync(ct)).Select(m => new PaymentMethodDto(m.Id, m.Code, m.Name, m.RequiresReceipt)).ToList();

    public async Task<PaymentAccountDto> CreateAccountAsync(SaveAccountCommand command, CancellationToken ct)
    {
        var method = await MethodByCodeAsync(command.MethodCode, ct);
        var account = PaymentAccount.Create(method.Id, command.AccountHolder, command.AccountNumber,
            command.QrImageUrl, command.Instructions, command.Active);
        await _accounts.AddAsync(account, ct);
        await _accounts.SaveChangesAsync(ct);
        return ToDto(account, await MethodsByIdAsync(ct));
    }

    public async Task<PaymentAccountDto> UpdateAccountAsync(short id, SaveAccountCommand command, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("PAYMENT_ACCOUNT_NOT_FOUND", $"No existe la cuenta {id}.");
        var method = await MethodByCodeAsync(command.MethodCode, ct);
        account.Update(method.Id, command.AccountHolder, command.AccountNumber, command.QrImageUrl,
            command.Instructions, command.Active);
        await _accounts.SaveChangesAsync(ct);
        return ToDto(account, await MethodsByIdAsync(ct));
    }

    // ------------------------------------------------------------------ apoyo

    public async Task<PaymentDto> GetAsync(long paymentId, CancellationToken ct)
    {
        var p = await LoadAsync(paymentId, ct);

        return new PaymentDto(
            p.Id, p.BookingId, p.PaymentAccountId, p.Amount, p.Status.ToString(),
            p.ProcessedAtUtc, p.ApprovedBy, p.RejectionReason,
            p.Receipts
                .Select(r => new ReceiptDto(
                    r.Id, r.FileUrl, r.TransactionReference, r.ReportedAmount, r.UploadedBy, r.UploadedAtUtc))
                .ToList());
    }

    private static PaymentStatus ParseStatus(string status) => status.Trim().ToUpperInvariant() switch
    {
        "PENDING" => PaymentStatus.Pending,
        "IN_REVIEW" => PaymentStatus.InReview,
        "APPROVED" => PaymentStatus.Approved,
        "REJECTED" => PaymentStatus.Rejected,
        "REFUNDED" => PaymentStatus.Refunded,
        _ => throw new DomainException("INVALID_STATUS", $"Estado de pago desconocido: {status}.")
    };

    public static string StatusCode(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "PENDING",
        PaymentStatus.InReview => "IN_REVIEW",
        PaymentStatus.Approved => "APPROVED",
        PaymentStatus.Rejected => "REJECTED",
        _ => "REFUNDED"
    };

    private async Task<PaymentMethodType> MethodByCodeAsync(string code, CancellationToken ct) =>
        (await _accounts.MethodTypesAsync(ct)).FirstOrDefault(m => m.Code == code?.Trim().ToUpperInvariant())
        ?? throw new DomainException("INVALID_PAYMENT_METHOD", $"Medio de pago desconocido: {code}.");

    private async Task<Dictionary<short, PaymentMethodType>> MethodsByIdAsync(CancellationToken ct) =>
        (await _accounts.MethodTypesAsync(ct)).ToDictionary(m => m.Id);

    private async Task<Dictionary<short, PaymentAccountDto>> AccountsByIdAsync(CancellationToken ct)
    {
        var methods = await MethodsByIdAsync(ct);
        return (await _accounts.ListAsync(onlyActive: false, ct)).ToDictionary(a => a.Id, a => ToDto(a, methods));
    }

    private async Task<PaymentView> ViewAsync(Payment payment, BookingInfo? booking, CancellationToken ct) =>
        View(payment, booking, await AccountsByIdAsync(ct));

    private static PaymentView View(Payment p, BookingInfo? booking, IReadOnlyDictionary<short, PaymentAccountDto> accounts)
    {
        var receipt = p.Receipts.OrderByDescending(r => r.UploadedAtUtc).FirstOrDefault();
        return new PaymentView(p.Id, StatusCode(p.Status), p.Amount, p.ProcessedAtUtc, p.RejectionReason,
            receipt?.TransactionReference, IsImage(receipt?.FileUrl) ? receipt!.FileUrl : null, receipt?.UploadedAtUtc, receipt?.UploadedBy,
            accounts.GetValueOrDefault(p.PaymentAccountId), booking);
    }

    // el pago manual no trae imagen; solo se manda la que sí es imagen
    private static bool IsImage(string? url) =>
        url is not null && (url.StartsWith("data:image/") || url.StartsWith("http"));

    private static PaymentAccountDto ToDto(PaymentAccount a, IReadOnlyDictionary<short, PaymentMethodType> methods)
    {
        var method = methods.GetValueOrDefault(a.PaymentMethodTypeId);
        return new PaymentAccountDto(a.Id, method?.Code ?? "", method?.Name ?? "", a.AccountHolder, a.AccountNumber,
            a.QrImageUrl, a.Instructions, a.IsActive, method?.RequiresReceipt ?? true);
    }

    private async Task<Payment> LoadAsync(long paymentId, CancellationToken ct) =>
        await _payments.GetByIdAsync(paymentId, ct)
            ?? throw new NotFoundException("PAYMENT_NOT_FOUND", $"No existe el pago {paymentId}.");

    // Cargar -> aplicar la regla del dominio -> guardar.
    private async Task ExecuteAsync(long paymentId, Action<Payment> action, CancellationToken ct)
    {
        var payment = await LoadAsync(paymentId, ct);
        action(payment);
        await _payments.SaveChangesAsync(ct);
    }
}
