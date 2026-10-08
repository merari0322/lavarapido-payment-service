using PaymentService.Application.PaymentAccounts;
using PaymentService.Application.Ports.Out.Integration;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>
/// Patrón Factory (assembler): construye PaymentView a partir del aggregate, su cuenta y la reserva.
/// Concentra en un solo lugar cómo se presenta un pago, para que comandos y consultas devuelvan
/// exactamente la misma forma.
/// </summary>
public sealed class PaymentViewFactory
{
    private readonly PaymentAccountReader _accounts;

    public PaymentViewFactory(PaymentAccountReader accounts)
    {
        _accounts = accounts;
    }

    public async Task<PaymentView> CreateAsync(Payment payment, BookingInfo? booking, CancellationToken ct) =>
        Create(payment, booking, await _accounts.ByIdAsync(ct));

    /// <summary>Varias vistas cargando las cuentas una sola vez (evita una consulta por pago).</summary>
    public async Task<IReadOnlyList<PaymentView>> CreateManyAsync(IEnumerable<Payment> payments,
        IReadOnlyDictionary<long, BookingInfo> bookings, CancellationToken ct)
    {
        var accounts = await _accounts.ByIdAsync(ct);
        return payments.Select(p => Create(p, bookings.GetValueOrDefault(p.BookingId), accounts)).ToList();
    }

    private static PaymentView Create(Payment payment, BookingInfo? booking,
        IReadOnlyDictionary<short, PaymentAccountDto> accounts)
    {
        var receipt = payment.LatestReceipt;
        return new PaymentView(
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
            booking);
    }
}
