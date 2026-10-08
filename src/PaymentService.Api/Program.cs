using Microsoft.AspNetCore.Server.Kestrel.Core;
using PaymentService.Api.Configuration;
using PaymentService.Api.Errors;
using PaymentService.Application;
using PaymentService.Api.Http;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Booking;

// Composition root: el único lugar donde se juntan todas las capas. Api conoce a Application (los
// casos de uso) y a Infrastructure (los adaptadores) solo para conectarlos aquí; el dominio y los
// casos de uso nunca dependen de la API ni de la infraestructura.

// Igual que los servicios Java: los valores salen de lavarapido-infra/.env (o de variables de
// entorno, que mandan). Así no hay contraseñas ni puertos quemados en appsettings.
EnvFile.Load();

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.WebHost.UseUrls($"http://0.0.0.0:{config["PAYMENT_PORT"] ?? "3005"}");
config.EnsurePaymentConnectionString();

// El comprobante y el QR viajan como imagen (data URL) en el cuerpo.
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);

builder.Services.AddJwtAuthentication(config);
builder.Services.AddConfiguredCors(config);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Casos de uso (puertos de entrada) y adaptadores (puertos de salida).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);

// Recursos del host que las capas internas usan pero no deciden: el reloj y la identidad del
// request en curso (Infrastructure la reenvía a booking-service).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAccessTokenProvider, HttpContextAccessTokenProvider>();

var app = builder.Build();

// IDs reales de los catálogos (tipos de descuento y de movimiento de puntos), leídos por código.
await app.Services.InitializeInfrastructureAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { service = "payment-service", status = "ok" }));
// misma ruta de salud que los servicios Java
app.MapGet("/actuator/health", () => Results.Ok(new { status = "UP" }));

app.Run();
