using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentService.Api;
using PaymentService.Application.Payments;
using PaymentService.Infrastructure;

// Igual que los servicios Java: los valores salen de lavarapido-infra/.env (o de variables de
// entorno, que mandan). Así no hay contraseñas ni puertos quemados en appsettings.
EnvFile.Load();

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.WebHost.UseUrls($"http://0.0.0.0:{config["PAYMENT_PORT"] ?? "3005"}");

// Base LavaRapido compartida (ADR-003); payment es dueño de los esquemas payment y promotion.
if (string.IsNullOrWhiteSpace(config.GetConnectionString("PaymentDb")))
{
    config["ConnectionStrings:PaymentDb"] =
        $"Server={config["DB_HOST"] ?? "localhost"},{config["DB_PORT"] ?? "1433"};" +
        $"Database={config["DB_NAME"] ?? "LavaRapido"};User Id={config["DB_USERNAME"] ?? "sa"};" +
        $"Password={config["DB_PASSWORD"]};TrustServerCertificate=True";
}

// JWT HS256 emitido por security-service (ADR-006): mismo secreto, emisor y audiencia.
var secret = config["JWT_SECRET"];
if (string.IsNullOrWhiteSpace(secret))
    throw new InvalidOperationException("JWT_SECRET is not set (lavarapido-infra/.env)");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // sub y roles tal cual vienen en el token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["JWT_ISSUER"] ?? "lavarapido-security-service",
            ValidateAudience = true,
            ValidAudience = config["JWT_AUDIENCE"] ?? "lavarapido-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            RoleClaimType = "roles",
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var origins = (config["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// el comprobante y el QR viajan como imagen en el cuerpo
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o =>
    o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddInfrastructure(config);
builder.Services.AddScoped<PaymentApplicationService>();
builder.Services.AddScoped<PaymentService.Application.Promotions.PromotionApplicationService>();
builder.Services.AddScoped<PaymentService.Application.Loyalty.LoyaltyApplicationService>();

var app = builder.Build();

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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
// misma ruta de salud que los servicios Java
app.MapGet("/actuator/health", () => Results.Ok(new { status = "UP" }));

app.Run();
