using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Payments;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<PaymentMethodType> PaymentMethodTypes => Set<PaymentMethodType>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<BookingPromotionRecord> BookingPromotions => Set<BookingPromotionRecord>();
    public DbSet<DiscountTypeRecord> DiscountTypes => Set<DiscountTypeRecord>();
    public DbSet<LoyaltyTransactionRecord> LoyaltyTransactions => Set<LoyaltyTransactionRecord>();
    public DbSet<LoyaltyMovementTypeRecord> LoyaltyMovementTypes => Set<LoyaltyMovementTypeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
