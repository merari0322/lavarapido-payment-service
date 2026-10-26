using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PaymentService.Api.Configuration;
using PaymentService.Api.Controllers;
using PaymentService.Application.Common;
using PaymentService.Application.Payments;
using PaymentService.Application.Ports.In;
using Xunit;

namespace PaymentService.Api.UnitTests;

/// <summary>
/// Adenda pago-reserva: GET /internal/v1/payments/bookings/{id}/reported solo responde con la llave
/// interna (X-Internal-Key = INTERNAL_API_KEY), como /internal/** en booking-service. El JWT de un
/// usuario no sirve. Se monta el controlador real con la misma configuración de autenticación.
/// </summary>
public sealed class InternalApiKeyTests
{
    private const string Secret = "test-secret-with-at-least-thirty-two-bytes!!";
    private const string Key = "llave-interna-de-prueba";
    private const string Path = "/internal/v1/payments/bookings/123/reported";

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(string? internalKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = Secret,
            ["INTERNAL_API_KEY"] = internalKey
        });
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(InternalPaymentsController).Assembly);
        builder.Services.AddSingleton<IPaymentQueryUseCases>(new ReportedBookings(123));
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? key, string? jwtRole = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (key is not null) request.Headers.Add("X-Internal-Key", key);
        if (jwtRole is not null) request.Headers.Authorization = new("Bearer", Token(jwtRole));
        return await client.SendAsync(request);
    }

    private static string Token(string role) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "lavarapido-security-service",
        Audience = "lavarapido-api",
        Expires = DateTime.UtcNow.AddMinutes(10),
        Claims = new Dictionary<string, object> { ["sub"] = "7", ["roles"] = new[] { role } },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            SecurityAlgorithms.HmacSha256)
    });

    [Fact]
    public async Task WithTheRightKey_AnswersWhetherTheBookingWasReported()
    {
        var (app, client) = await StartAsync(Key);
        await using var _ = app;

        var response = await GetAsync(client, Path, Key);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal("{\"bookingId\":123,\"reported\":true}", json);

        var other = await (await GetAsync(client, "/internal/v1/payments/bookings/456/reported", Key))
            .Content.ReadFromJsonAsync<BookingReportedDto>();
        Assert.Equal(new BookingReportedDto(456, false), other);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("llave-equivocada")]
    [InlineData("LLAVE-INTERNA-DE-PRUEBA")]
    public async Task WithoutTheKeyOrWithAWrongOne_Is401(string? key)
    {
        var (app, client) = await StartAsync(Key);
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(client, Path, key)).StatusCode);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("CLIENT")]
    public async Task AUserTokenDoesNotOpenTheInternalRoute(string role)
    {
        var (app, client) = await StartAsync(Key);
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(client, Path, null, role)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task WhenTheServiceHasNoKeyConfigured_NobodyGetsIn(string? configured)
    {
        // sin INTERNAL_API_KEY la ruta queda cerrada, aunque llegue el encabezado
        var (app, client) = await StartAsync(configured);
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(client, Path, "cualquiera")).StatusCode);
    }

    [Fact]
    public async Task TheKeyDoesNotOpenTheUserRoutes()
    {
        var (app, client) = await StartAsync(Key);
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(client, "/api/v1/admin/payments", Key)).StatusCode);
    }

    /// <summary>Caso de uso falso: solo la reserva indicada tiene un pago reportado.</summary>
    private sealed class ReportedBookings : IPaymentQueryUseCases
    {
        private readonly long _reportedBookingId;

        public ReportedBookings(long reportedBookingId) => _reportedBookingId = reportedBookingId;

        public Task<BookingReportedDto> BookingReportedAsync(long bookingId, CancellationToken ct) =>
            Task.FromResult(new BookingReportedDto(bookingId, bookingId == _reportedBookingId));

        public Task<IReadOnlyList<PaymentDto>> MineAsync(PaymentHistoryFilter filter, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentDto> GetMineAsync(long paymentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<BookingPaymentStateDto>> MyBookingsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PaymentDto>> ListAsync(string? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<PageDto<PaymentDto>> ListPageAsync(string? status, int page, int size, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentDto> GetAsync(long paymentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<AmountDueDto> AmountDueAsync(long bookingId, CancellationToken ct) => throw new NotSupportedException();
    }
}
