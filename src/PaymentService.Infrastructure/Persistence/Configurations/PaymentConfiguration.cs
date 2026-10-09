using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo del aggregate Payment a payment.payment.</summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment", "payment");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("payment_id").ValueGeneratedOnAdd();

        builder.Property(p => p.BookingId).HasColumnName("booking_id");
        builder.Property(p => p.PaymentAccountId).HasColumnName("payment_account_id");
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(12, 2);
        // IDs fijos del catálogo payment_status (ver PaymentStatusIds).
        builder.Property(p => p.Status).HasColumnName("payment_status_id")
            .HasConversion(status => PaymentStatusIds.ToId(status), id => PaymentStatusIds.ToStatus(id));
        builder.Property(p => p.ProcessedAtUtc).HasColumnName("processed_at");
        builder.Property(p => p.ApprovedBy).HasColumnName("approved_by");
        builder.Property(p => p.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(Payment.MaxRejectionReasonLength);
        builder.HasAuditColumns();

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.LatestReceipt);

        // Los comprobantes se cargan y guardan siempre junto con su pago (son parte del aggregate);
        // EF escribe directamente en la lista privada _receipts.
        builder.HasMany(p => p.Receipts).WithOne().HasForeignKey("PaymentId");
        builder.Navigation(p => p.Receipts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Mapeo de PaymentReceipt a payment.payment_receipt.</summary>
internal sealed class PaymentReceiptConfiguration : IEntityTypeConfiguration<PaymentReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentReceipt> builder)
    {
        builder.ToTable("payment_receipt", "payment");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("payment_receipt_id").ValueGeneratedOnAdd();

        builder.Property<long>("PaymentId").HasColumnName("payment_id");
        // NVARCHAR(MAX) desde la migración 016: guarda la imagen como data URL.
        builder.Property(r => r.FileUrl).HasColumnName("file_url");
        builder.Property(r => r.TransactionReference).HasColumnName("transaction_reference").HasMaxLength(100);
        builder.Property(r => r.ReportedAmount).HasColumnName("reported_amount").HasPrecision(12, 2);
        builder.Property(r => r.UploadedAtUtc).HasColumnName("uploaded_at");
        builder.Property(r => r.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(r => r.ReviewedAtUtc).HasColumnName("reviewed_at");
        builder.Property(r => r.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(r => r.ReviewComment).HasColumnName("review_comment").HasMaxLength(300);
        builder.HasAuditColumns();

        builder.Ignore(r => r.HasImage);
    }
}
