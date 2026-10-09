using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PaymentService.Api.Configuration;

/// <summary>
/// Configuración propia de la capa HTTP (base de datos, JWT, CORS), sacada de Program.cs para que
/// este quede como una lista corta y legible de pasos de arranque.
/// </summary>
internal static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Si no viene ConnectionStrings:PaymentDb, la arma con las variables DB_* de
    /// lavarapido-infra/.env (base LavaRapido compartida, ADR-003). Así no hay contraseñas en appsettings.
    /// </summary>
    public static void EnsurePaymentConnectionString(this ConfigurationManager config)
    {
        if (!string.IsNullOrWhiteSpace(config.GetConnectionString("PaymentDb"))) return;

        config["ConnectionStrings:PaymentDb"] =
            $"Server={config["DB_HOST"] ?? "localhost"},{config["DB_PORT"] ?? "1433"};" +
            $"Database={config["DB_NAME"] ?? "LavaRapido"};User Id={config["DB_USERNAME"] ?? "sa"};" +
            $"Password={config["DB_PASSWORD"]};TrustServerCertificate=True";
    }

    /// <summary>JWT HS256 emitido por security-service (ADR-006): mismo secreto, emisor y audiencia.</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var secret = config["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JWT_SECRET is not set (lavarapido-infra/.env)");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
        services.AddAuthorization();
        return services;
    }

    /// <summary>CORS para la web (orígenes separados por coma en CORS_ALLOWED_ORIGINS).</summary>
    public static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration config)
    {
        var origins = (config["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:4200")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        return services;
    }
}
