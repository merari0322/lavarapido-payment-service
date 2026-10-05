using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PaymentService.Application.Payments;
using PaymentService.Domain.Common;

namespace PaymentService.Infrastructure.Booking;

/// <summary>
/// Adaptador REST hacia booking-service. Reenvía el token del usuario: booking aplica sus propias
/// reglas (un cliente solo ve sus reservas, /admin solo el admin).
/// </summary>
public sealed class BookingServiceDirectory : IBookingDirectory
{
    private readonly HttpClient _http;

    public BookingServiceDirectory(HttpClient http)
    {
        _http = http;
    }

    public Task<BookingInfo?> GetForCustomerAsync(long bookingId, string bearerToken, CancellationToken ct) =>
        GetAsync($"bookings/{bookingId}", bearerToken, ct);

    public Task<BookingInfo?> GetForAdminAsync(long bookingId, string bearerToken, CancellationToken ct) =>
        GetAsync($"admin/bookings/{bookingId}", bearerToken, ct);

    public async Task<IReadOnlyList<BookingInfo>> MineAsync(string bearerToken, CancellationToken ct)
    {
        using var request = Request("bookings/me", bearerToken);
        using var response = await SendAsync(request, ct);
        var bookings = await response.Content.ReadFromJsonAsync<List<BookingJson>>(ct) ?? new();
        return bookings.Select(ToInfo).ToList();
    }

    private async Task<BookingInfo?> GetAsync(string path, string bearerToken, CancellationToken ct)
    {
        using var request = Request(path, bearerToken);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var booking = await response.Content.ReadFromJsonAsync<BookingJson>(ct);
        return booking is null ? null : ToInfo(booking);
    }

    private static HttpRequestMessage Request(string path, string bearerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new DomainException("BOOKING_SERVICE_UNAVAILABLE", "No se pudo consultar la reserva en este momento.");
        }
        if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new DomainException("BOOKING_SERVICE_UNAVAILABLE", "No se pudo consultar la reserva en este momento.");
        }
        return response;
    }

    private static BookingInfo ToInfo(BookingJson b)
    {
        var vehicle = b.Vehicle;
        var name = vehicle is null ? "" : $"{vehicle.Brand} {vehicle.Model}".Trim();
        if (vehicle is not null && name.Length == 0) name = vehicle.VehicleTypeName ?? "";
        return new BookingInfo(b.Id, b.Code ?? "", b.Status ?? "", b.Total, b.Date ?? "", b.StartTime ?? "",
            string.Join(", ", (b.Services ?? new()).Select(s => s.Name)), name,
            vehicle?.LicensePlateFormatted ?? "", b.OwnerUserId);
    }

    // forma del JSON de booking-service (solo los campos que se usan)
    private sealed record BookingJson(long Id, string? Code, string? Status, decimal Total, string? Date,
        string? StartTime, List<LineJson>? Services, VehicleJson? Vehicle, long? OwnerUserId);

    private sealed record LineJson(string Name);

    private sealed record VehicleJson(string? Brand, string? Model, string? VehicleTypeName, string? LicensePlateFormatted);
}
