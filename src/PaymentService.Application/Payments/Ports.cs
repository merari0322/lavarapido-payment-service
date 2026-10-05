using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments;

/// <summary>Quién llama, leído del JWT ya verificado (sub y token para reenviarlo a booking).</summary>
public sealed record Caller(long UserId, string BearerToken);

/// <summary>Lo que payment necesita saber de una reserva; el dueño es booking-service.</summary>
public sealed record BookingInfo(
    long Id,
    string Code,
    string Status,
    decimal Total,
    string Date,
    string StartTime,
    string Services,
    string Vehicle,
    string Plate,
    long? OwnerUserId);

/// <summary>
/// Puerto hacia booking-service (REST con el token del usuario, ADR-004). El cliente solo ve
/// sus reservas; el admin, todas. El monto a pagar sale de aquí, nunca del navegador.
/// </summary>
public interface IBookingDirectory
{
    Task<BookingInfo?> GetForCustomerAsync(long bookingId, string bearerToken, CancellationToken ct);
    Task<IReadOnlyList<BookingInfo>> MineAsync(string bearerToken, CancellationToken ct);
    Task<BookingInfo?> GetForAdminAsync(long bookingId, string bearerToken, CancellationToken ct);
}

public interface IPaymentAccountRepository
{
    Task<IReadOnlyList<PaymentAccount>> ListAsync(bool onlyActive, CancellationToken ct);
    Task<PaymentAccount?> GetByIdAsync(short id, CancellationToken ct);
    Task<IReadOnlyList<PaymentMethodType>> MethodTypesAsync(CancellationToken ct);
    Task AddAsync(PaymentAccount account, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
