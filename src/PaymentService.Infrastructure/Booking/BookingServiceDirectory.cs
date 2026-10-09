using System.Net;
using System.Net.Http.Json;
using PaymentService.Application.Common;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Ports.Out.Integration;

namespace PaymentService.Infrastructure.Booking;

/// <summary>
/// Adaptador REST del puerto IBookingDirectory hacia booking-service. La identidad del usuario la
/// agrega AccessTokenForwardingHandler, para que booking aplique sus propias reglas (un cliente
/// solo ve sus reservas, /admin solo el admin). Funciona además como Anti-Corruption Layer:
/// traduce el JSON de booking a BookingInfo, así un cambio en ese JSON solo toca este archivo.
/// </summary>
internal sealed class BookingServiceDirectory : IBookingDirectory
{
    private readonly HttpClient _http;

    public BookingServiceDirectory(HttpClient http)
    {
        _http = http;
    }

    public Task<BookingInfo?> GetForCustomerAsync(long bookingId, CancellationToken ct) =>
        GetAsync($"bookings/{bookingId}", ct);

    public Task<BookingInfo?> GetForAdminAsync(long bookingId, CancellationToken ct) =>
        GetAsync($"admin/bookings/{bookingId}", ct);

    public async Task<IReadOnlyList<BookingInfo>> MineAsync(CancellationToken ct)
    {
        using var response = await SendAsync("bookings/me", ct);
        var bookings = await response.Content.ReadFromJsonAsync<List<BookingJson>>(ct) ?? new();
        return bookings.Select(ToInfo).ToList();
    }

    private async Task<BookingInfo?> GetAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var booking = await response.Content.ReadFromJsonAsync<BookingJson>(ct);
        return booking is null ? null : ToInfo(booking);
    }

    /// <summary>
    /// Hace el GET; 404 se devuelve tal cual (la reserva no existe o no es del que llama) y
    /// cualquier otro fallo (red, tiempo de espera, 5xx) se traduce a ServiceUnavailableException.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(path, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw Unavailable();
        }

        if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw Unavailable();
        }
        return response;
    }

    private static ServiceUnavailableException Unavailable() =>
        new(ErrorCodes.BookingServiceUnavailable, "No se pudo consultar la reserva en este momento.");

    private static BookingInfo ToInfo(BookingJson b)
    {
        var vehicle = b.Vehicle;
        var vehicleName = vehicle is null ? string.Empty : $"{vehicle.Brand} {vehicle.Model}".Trim();
        if (vehicle is not null && vehicleName.Length == 0) vehicleName = vehicle.VehicleTypeName ?? string.Empty;

        return new BookingInfo(b.Id, b.Code ?? string.Empty, b.Status ?? string.Empty, b.Total, b.Date ?? string.Empty,
            b.StartTime ?? string.Empty, string.Join(", ", (b.Services ?? new()).Select(s => s.Name)), vehicleName,
            vehicle?.LicensePlateFormatted ?? string.Empty, b.OwnerUserId, b.TotalLoyaltyPoints);
    }

    // Forma del JSON de booking-service (solo los campos que se usan).
    private sealed record BookingJson(long Id, string? Code, string? Status, decimal Total, string? Date,
        string? StartTime, List<LineJson>? Services, VehicleJson? Vehicle, long? OwnerUserId, int TotalLoyaltyPoints);

    private sealed record LineJson(string Name);

    private sealed record VehicleJson(string? Brand, string? Model, string? VehicleTypeName, string? LicensePlateFormatted);
}
