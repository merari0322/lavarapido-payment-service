using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Loyalty;
using PaymentService.Domain.PaymentAccounts;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// DbContext de EF Core sobre los esquemas payment y promotion de la base compartida (ADR-003).
/// Las tablas las crea Liquibase (db/changelog), no EF: aquí solo se describe cómo se mapean las
/// entidades del dominio a columnas existentes (ver Configurations/).
/// </summary>
public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<PaymentMethodType> PaymentMethodTypes => Set<PaymentMethodType>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionRedemption> PromotionRedemptions => Set<PromotionRedemption>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
}
