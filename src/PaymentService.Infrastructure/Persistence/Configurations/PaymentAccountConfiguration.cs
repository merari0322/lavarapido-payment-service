using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.PaymentAccounts;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de PaymentAccount a payment.payment_account.</summary>
internal sealed class PaymentAccountConfiguration : IEntityTypeConfiguration<PaymentAccount>
{
    public void Configure(EntityTypeBuilder<PaymentAccount> builder)
    {
        builder.ToTable("payment_account", "payment");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("payment_account_id").ValueGeneratedOnAdd();

        builder.Property(a => a.PaymentMethodTypeId).HasColumnName("payment_method_type_id");
        builder.Property(a => a.AccountHolder).HasColumnName("account_holder").HasMaxLength(120);
        builder.Property(a => a.AccountNumber).HasColumnName("account_number").HasMaxLength(50);
        // NVARCHAR(MAX) desde la migración 016: guarda el QR como data URL.
        builder.Property(a => a.QrImageUrl).HasColumnName("qr_image_url");
        builder.Property(a => a.Instructions).HasColumnName("instructions").HasMaxLength(300);
        builder.Property(a => a.IsActive).HasColumnName("is_active");
        builder.HasAuditColumns();
    }
}

/// <summary>Mapeo de PaymentMethodType a payment.payment_method_type (catálogo de solo lectura).</summary>
internal sealed class PaymentMethodTypeConfiguration : IEntityTypeConfiguration<PaymentMethodType>
{
    public void Configure(EntityTypeBuilder<PaymentMethodType> builder)
    {
        builder.ToTable("payment_method_type", "payment");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("payment_method_type_id");

        builder.Property(m => m.Code).HasColumnName("code").HasMaxLength(30);
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(60);
        builder.Property(m => m.RequiresAccount).HasColumnName("requires_account");
        builder.Property(m => m.RequiresReceipt).HasColumnName("requires_receipt");
        builder.Property(m => m.DisplayOrder).HasColumnName("display_order");
        builder.Property(m => m.IsActive).HasColumnName("is_active");
        builder.HasAuditColumns();
    }
}
