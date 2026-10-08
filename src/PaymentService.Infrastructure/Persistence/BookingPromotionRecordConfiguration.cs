using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentService.Infrastructure.Persistence;

public class BookingPromotionRecordConfiguration : IEntityTypeConfiguration<BookingPromotionRecord>
{
    public void Configure(EntityTypeBuilder<BookingPromotionRecord> builder)
    {
        builder.ToTable("booking_promotion", "promotion");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("booking_promotion_id").ValueGeneratedOnAdd();
        builder.Property(b => b.BookingId).HasColumnName("booking_id");
        builder.Property(b => b.PromotionId).HasColumnName("promotion_id");
        builder.Property(b => b.AppliedAmount).HasColumnName("applied_amount").HasColumnType("decimal(12,2)");
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property<DateTime?>("DeletedAt").HasColumnName("deleted_at");
        builder.HasQueryFilter(b => EF.Property<DateTime?>(b, "DeletedAt") == null);
    }
}
