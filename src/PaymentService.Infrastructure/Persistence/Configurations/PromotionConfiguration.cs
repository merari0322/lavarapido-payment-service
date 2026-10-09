using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo del aggregate Promotion a promotion.promotion. is_public y min_completed_booking no se
/// mapean (el dominio no los evalúa todavía): al insertar, la base pone sus valores por defecto
/// (pública, 0 reservas) y al actualizar EF no los toca.
/// </summary>
internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotion", "promotion");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("promotion_id").ValueGeneratedOnAdd();

        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(30);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(300);
        builder.Property(p => p.Price).HasColumnName("price").HasPrecision(12, 2);
        builder.Property(p => p.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(p => p.Icon).HasColumnName("icon").HasMaxLength(40);
        builder.Property(p => p.Featured).HasColumnName("featured");
        builder.Property(p => p.ValidFrom).HasColumnName("valid_from");
        builder.Property(p => p.ValidTo).HasColumnName("valid_to");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.RequiredPoints).HasColumnName("required_points");

        // El ID real de cada tipo se resuelve por código al arrancar (ver CatalogIds).
        builder.Property(p => p.DiscountType).HasColumnName("discount_type_id")
            .HasConversion(type => CatalogIds.ToId(type), id => CatalogIds.ToDiscountType(id));
        builder.Property(p => p.DiscountValue).HasColumnName("discount_value").HasPrecision(12, 2);
        builder.Property(p => p.MaxDiscountAmount).HasColumnName("max_discount_amount").HasPrecision(12, 2);
        builder.Property(p => p.MinPurchaseAmount).HasColumnName("min_purchase_amount").HasPrecision(12, 2);
        builder.Property(p => p.MaxRedemptions).HasColumnName("max_redemptions");
        builder.Property(p => p.MaxRedemptionsPerCustomer).HasColumnName("max_redemptions_per_customer");

        // Los beneficios se guardan como una línea por beneficio, a través del campo privado
        // nullable (sin beneficios = NULL). El ValueComparer le dice a EF cómo detectar que la lista
        // cambió (sin él, editar los beneficios no se guardaría).
        builder.Ignore(p => p.Benefits);
        builder.Property<IReadOnlyList<string>?>("_benefits")
            .HasColumnName("benefits")
            .HasMaxLength(600)
            .IsRequired(false)
            .HasConversion(
                list => string.Join('\n', list!),
                raw => raw.Split('\n', StringSplitOptions.None).ToList(),
                new ValueComparer<IReadOnlyList<string>?>(
                    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                    list => list == null ? 0 : list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    list => list == null ? null : list.ToList()));

        builder.HasAuditColumns();
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.DiscountPercent);
    }
}

/// <summary>Mapeo de PromotionRedemption a promotion.booking_promotion.</summary>
internal sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> builder)
    {
        builder.ToTable("booking_promotion", "promotion");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("booking_promotion_id").ValueGeneratedOnAdd();

        builder.Property(r => r.BookingId).HasColumnName("booking_id");
        builder.Property(r => r.PromotionId).HasColumnName("promotion_id");
        builder.Property(r => r.AppliedAmount).HasColumnName("applied_amount").HasPrecision(12, 2);
        builder.Property(r => r.RedeemedBy).HasColumnName("created_by");
        builder.Property(r => r.RedeemedAtUtc).HasColumnName("created_at");
        builder.HasAuditColumns();
    }
}
