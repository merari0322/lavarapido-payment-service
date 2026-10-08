using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Application.Ports.Out.Persistence;
using PaymentService.Domain.Common;
using PaymentService.Domain.PaymentAccounts;

namespace PaymentService.Application.Payments;

/// <summary>
/// Precondiciones para registrar un pago (o cambiar lo que se va a pagar) que dependen de datos
/// fuera del aggregate Payment: el estado de la reserva, los otros pagos de la misma reserva, la
/// cuenta elegida y el control antifraude. Antes estaban copiadas en cada caso de uso; aquí cada
/// regla existe una sola vez (responsabilidad única, sin duplicación).
/// </summary>
public sealed class BookingPaymentGuard
{
    // Estados de reserva en los que tiene sentido pagar (una cancelada o no asistida no se paga).
    private static readonly HashSet<string> PayableBookingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "CONFIRMED", "IN_PROGRESS", "COMPLETED" };

    private readonly IPaymentRepository _payments;
    private readonly IPaymentAccountRepository _accounts;

    public BookingPaymentGuard(IPaymentRepository payments, IPaymentAccountRepository accounts)
    {
        _payments = payments;
        _accounts = accounts;
    }

    /// <summary>
    /// La reserva está en un estado pagable y todavía no tiene un pago aprobado. Con
    /// allowOpenPayment = false tampoco puede tener uno pendiente o en revisión (el cliente no
    /// reporta dos veces ni canjea un cupón cuando el monto ya quedó fijado en un pago reportado).
    /// </summary>
    public async Task EnsureBookingIsUnpaidAsync(BookingInfo booking, bool allowOpenPayment, CancellationToken ct)
    {
        if (!PayableBookingStatuses.Contains(booking.Status))
            throw new ConflictException(ErrorCodes.BookingNotPayable, "Esta reserva no se puede pagar en su estado actual.");
        if (await _payments.HasApprovedPaymentAsync(booking.Id, ct))
            throw new ConflictException(ErrorCodes.PaymentAlreadyApproved, "La reserva ya tiene un pago aprobado.");
        if (!allowOpenPayment && await _payments.HasOpenPaymentAsync(booking.Id, ct))
            throw new ConflictException(ErrorCodes.PaymentAlreadyReported, "La reserva ya tiene un pago en revisión.");
    }

    /// <summary>
    /// Devuelve la cuenta si existe y está activa. Cuando el pago lo reporta el cliente
    /// (forCustomerReport) el medio además debe exigir comprobante: el efectivo solo lo registra el
    /// admin en el lavadero.
    /// </summary>
    public async Task<PaymentAccount> GetUsableAccountAsync(short accountId, bool forCustomerReport, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(accountId, ct);
        if (account is null || !account.IsActive)
            throw InvalidAccount("La cuenta de pago no existe o no está activa.");

        if (forCustomerReport)
        {
            var method = await _accounts.GetMethodTypeAsync(account.PaymentMethodTypeId, ct);
            if (method is null || !method.RequiresReceipt)
                throw InvalidAccount("Este medio de pago se paga directamente en el lavadero.");
        }
        return account;
    }

    /// <summary>
    /// Control antifraude del modelo de datos (índice ix_payment_receipt_reference): la misma
    /// referencia no puede respaldar pagos de dos reservas distintas. Sobre la misma reserva sí se
    /// permite (el cliente vuelve a subir el comprobante tras un rechazo).
    /// </summary>
    public async Task EnsureReferenceNotReusedAsync(string? transactionReference, long bookingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionReference)) return;
        if (await _payments.IsTransactionReferenceUsedElsewhereAsync(transactionReference.Trim(), bookingId, ct))
            throw new ConflictException(ErrorCodes.TransactionReferenceReused,
                "Esa referencia de transacción ya se usó para pagar otra reserva.");
    }

    private static DomainException InvalidAccount(string message) => new(ErrorCodes.InvalidPaymentAccount, message);
}
