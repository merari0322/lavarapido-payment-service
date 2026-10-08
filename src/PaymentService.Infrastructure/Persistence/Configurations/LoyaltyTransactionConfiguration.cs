using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Loyalty;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de LoyaltyTransaction a payment.loyalty_transaction (el ledger de puntos).</summary>
internal sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("loyalty_transaction", "payment");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("loyalty_transaction_id").ValueGeneratedOnAdd();

        builder.Property(t => t.CustomerId).HasColumnName("customer_id");
        // El ID real de cada tipo se resuelve por código al arrancar (ver CatalogIds).
        builder.Property(t => t.MovementType).HasColumnName("loyalty_movement_type_id")
            .HasConversion(type => CatalogIds.ToId(type), id => CatalogIds.ToMovementType(id));
        builder.Property(t => t.BookingId).HasColumnName("booking_id");
        builder.Property(t => t.Points).HasColumnName("points");
        builder.Property(t => t.BalanceAfter).HasColumnName("balance_after");
        builder.Property(t => t.ExpiresOn).HasColumnName("expires_on");
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(200);
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at");
        builder.HasAuditColumns();
    }
}
