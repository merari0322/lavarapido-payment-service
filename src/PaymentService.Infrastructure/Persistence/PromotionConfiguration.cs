using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotion", "promotion");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("promotion_id").ValueGeneratedOnAdd();
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(30);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(300);
        builder.Property(p => p.Price).HasColumnName("price").HasColumnType("decimal(12,2)");
        builder.Property(p => p.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(p => p.Icon).HasColumnName("icon").HasMaxLength(40);
        builder.Property(p => p.Featured).HasColumnName("featured");
        builder.Property(p => p.ValidFrom).HasColumnName("valid_from");
        builder.Property(p => p.ValidTo).HasColumnName("valid_to");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.RequiredPoints).HasColumnName("required_points");

        // DiscountPercent SI es del agregado (es el descuento real del cupón) y se persiste en la
        // misma columna discount_value que antes llenaba el hack de PACKAGE (ver comentario de la
        // clase de dominio y PromotionRepository.FillLegacyColumnsAsync, que solo necesita seguir
        // llenando discount_type_id porque esa columna no tiene propiedad real en el dominio).
        builder.Property(p => p.DiscountPercent)
            .HasColumnName("discount_value")
            .HasColumnType("decimal(12,2)")
            .HasConversion(percent => (decimal)percent, value => (int)value);
        builder.Property<short>("DiscountTypeId").HasColumnName("discount_type_id");
        builder.Property<decimal>("MinPurchaseAmount").HasColumnName("min_purchase_amount").HasColumnType("decimal(12,2)");
        builder.Property<int>("MinCompletedBooking").HasColumnName("min_completed_booking");
        builder.Property<bool>("IsPublic").HasColumnName("is_public");

        builder.Property(p => p.Benefits)
            .HasColumnName("benefits")
            .HasMaxLength(600)
            .HasConversion(
                list => list.Count == 0 ? null : string.Join('\n', list),
                raw => string.IsNullOrWhiteSpace(raw) ? new List<string>() : raw.Split(new[] { '\n' }, StringSplitOptions.None).ToList());

        // las borradas (soft delete) no se ven
        builder.Property<DateTime?>("DeletedAt").HasColumnName("deleted_at");
        builder.Property<long?>("DeletedBy").HasColumnName("deleted_by");
        builder.HasQueryFilter(p => EF.Property<DateTime?>(p, "DeletedAt") == null);
    }
}
