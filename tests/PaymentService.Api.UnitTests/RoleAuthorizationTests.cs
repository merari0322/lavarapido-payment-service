using System.Net;
using System.Security.Claims;
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
using Xunit;

namespace PaymentService.Api.UnitTests;

/// <summary>
/// F-04: cada ruta de los 5 controladores responde 403 al rol que no le corresponde. Se montan los
/// controladores reales con la misma configuración JWT del servicio; la autorización corta antes de
/// crear el controlador, así que no hace falta la base de datos ni los casos de uso.
/// </summary>
public sealed class RoleAuthorizationTests : IAsyncLifetime
{
    private const string Secret = "test-secret-with-at-least-thirty-two-bytes!!";
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_SECRET"] = Secret });
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(LoyaltyController).Assembly);
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static string Token(string role) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "lavarapido-security-service",
        Audience = "lavarapido-api",
        Expires = DateTime.UtcNow.AddMinutes(10),
        Claims = new Dictionary<string, object> { ["sub"] = "7", ["roles"] = new[] { role } },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            SecurityAlgorithms.HmacSha256)
    });

    private async Task<HttpStatusCode> Call(string method, string path, string role)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new("Bearer", Token(role));
        return (await _client.SendAsync(request)).StatusCode;
    }

    // rutas solo del administrador: AdminPaymentsController, AdminPaymentAccountsController y PromotionsController
    [Theory]
    [InlineData("GET", "/api/v1/admin/payments")]
    [InlineData("GET", "/api/v1/admin/payments/5")]
    [InlineData("GET", "/api/v1/admin/payments/amount-due?bookingId=5")]
    [InlineData("POST", "/api/v1/admin/payments")]
    [InlineData("POST", "/api/v1/admin/payments/5/approve")]
    [InlineData("POST", "/api/v1/admin/payments/5/reject")]
    [InlineData("POST", "/api/v1/admin/payments/5/refund")]
    [InlineData("GET", "/api/v1/admin/payment-accounts")]
    [InlineData("GET", "/api/v1/admin/payment-methods")]
    [InlineData("POST", "/api/v1/admin/payment-accounts")]
    [InlineData("PUT", "/api/v1/admin/payment-accounts/1")]
    [InlineData("GET", "/api/v1/admin/promotions")]
    [InlineData("GET", "/api/v1/admin/promotions/metrics")]
    [InlineData("POST", "/api/v1/admin/promotions")]
    [InlineData("PUT", "/api/v1/admin/promotions/1")]
    [InlineData("PATCH", "/api/v1/admin/promotions/1/active")]
    [InlineData("DELETE", "/api/v1/admin/promotions/1")]
    public async Task AdminRoutesAreForbiddenToClientsAndOperators(string method, string path)
    {
        Assert.Equal(HttpStatusCode.Forbidden, await Call(method, path, "CLIENT"));
        Assert.Equal(HttpStatusCode.Forbidden, await Call(method, path, "OPERATOR"));
    }

    // rutas solo del cliente: LoyaltyController y las de pago propio de PaymentsController
    [Theory]
    [InlineData("GET", "/api/v1/loyalty/balance")]
    [InlineData("GET", "/api/v1/loyalty/promotions")]
    [InlineData("POST", "/api/v1/loyalty/redeem")]
    [InlineData("GET", "/api/v1/loyalty/redemptions/7")]
    [InlineData("DELETE", "/api/v1/loyalty/redemptions/7/1")]
    [InlineData("POST", "/api/v1/payments")]
    [InlineData("GET", "/api/v1/payments/me")]
    [InlineData("GET", "/api/v1/payments/me/bookings")]
    [InlineData("GET", "/api/v1/payments/5")]
    public async Task ClientRoutesAreForbiddenToAdminsAndOperators(string method, string path)
    {
        Assert.Equal(HttpStatusCode.Forbidden, await Call(method, path, "ADMIN"));
        Assert.Equal(HttpStatusCode.Forbidden, await Call(method, path, "OPERATOR"));
    }

    [Fact]
    public async Task WithoutATokenTheAnswerIs401()
    {
        var response = await _client.GetAsync("/api/v1/admin/payments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
