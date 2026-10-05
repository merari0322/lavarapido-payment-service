using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public class PaymentAccountConfiguration : IEntityTypeConfiguration<PaymentAccount>
{
    public void Configure(EntityTypeBuilder<PaymentAccount> builder)
    {
        builder.ToTable("payment_account", "payment");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("payment_account_id").ValueGeneratedOnAdd();
        builder.Property(a => a.PaymentMethodTypeId).HasColumnName("payment_method_type_id");
        builder.Property(a => a.AccountHolder).HasColumnName("account_holder").HasMaxLength(120);
        builder.Property(a => a.AccountNumber).HasColumnName("account_number").HasMaxLength(50);
        builder.Property(a => a.QrImageUrl).HasColumnName("qr_image_url");
        builder.Property(a => a.Instructions).HasColumnName("instructions").HasMaxLength(300);
        builder.Property(a => a.IsActive).HasColumnName("is_active");
        // las borradas (soft delete) no se ven
        builder.Property<DateTime?>("DeletedAt").HasColumnName("deleted_at");
        builder.HasQueryFilter(a => EF.Property<DateTime?>(a, "DeletedAt") == null);
    }
}

public class PaymentMethodTypeConfiguration : IEntityTypeConfiguration<PaymentMethodType>
{
    public void Configure(EntityTypeBuilder<PaymentMethodType> builder)
    {
        builder.ToTable("payment_method_type", "payment");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("payment_method_type_id");
        builder.Property(m => m.Code).HasColumnName("code");
        builder.Property(m => m.Name).HasColumnName("name");
        builder.Property(m => m.RequiresReceipt).HasColumnName("requires_receipt");
    }
}
