using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Assembler: construye PaymentDto a partir del aggregate, su cuenta y la reserva. Concentra en un
/// solo lugar cómo se presenta un pago, para que comandos y consultas devuelvan exactamente la
/// misma forma. Aquí también se traduce BookingInfo (modelo del puerto hacia booking-service) al
/// contrato propio PaymentBookingDto.
/// </summary>
public sealed class PaymentDtoAssembler
{
    private readonly PaymentAccountReader _accounts;

    public PaymentDtoAssembler(PaymentAccountReader accounts)
    {
        _accounts = accounts;
    }

    public async Task<PaymentDto> ToDtoAsync(Payment payment, BookingInfo? booking, CancellationToken ct) =>
        ToDto(payment, booking, await _accounts.ByIdAsync(ct));

    /// <summary>Varios pagos cargando las cuentas una sola vez (evita una consulta por pago).</summary>
    public async Task<IReadOnlyList<PaymentDto>> ToDtosAsync(IEnumerable<Payment> payments,
        IReadOnlyDictionary<long, BookingInfo> bookings, CancellationToken ct)
    {
        var accounts = await _accounts.ByIdAsync(ct);
        return payments.Select(p => ToDto(p, bookings.GetValueOrDefault(p.BookingId), accounts)).ToList();
    }

    private static PaymentDto ToDto(Payment payment, BookingInfo? booking,
        IReadOnlyDictionary<short, PaymentAccountDto> accounts)
    {
        var receipt = payment.LatestReceipt;
        return new PaymentDto(
            payment.Id,
            payment.Status.ToCode(),
            payment.Amount,
            payment.ProcessedAtUtc,
            payment.RejectionReason,
            receipt?.TransactionReference,
            // el pago en persona guarda un marcador en lugar de imagen: no se manda a la web
            receipt is { HasImage: true } ? receipt.FileUrl : null,
            receipt?.UploadedAtUtc,
            receipt?.UploadedBy,
            accounts.GetValueOrDefault(payment.PaymentAccountId),
            booking is null ? null : ToDto(booking));
    }

    private static PaymentBookingDto ToDto(BookingInfo b) =>
        new(b.Id, b.Code, b.Status, b.Total, b.Date, b.StartTime, b.Services, b.Vehicle, b.Plate, b.OwnerUserId);
}
