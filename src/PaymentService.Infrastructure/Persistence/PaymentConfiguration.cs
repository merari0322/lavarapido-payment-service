using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Infrastructure.Persistence;
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment", "payment");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("payment_id").ValueGeneratedOnAdd();

        builder.Property(p => p.BookingId).HasColumnName("booking_id");
        builder.Property(p => p.PaymentAccountId).HasColumnName("payment_account_id");
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(12, 2);
        builder.Property(p => p.Status).HasColumnName("payment_status_id").HasConversion<short>();
        builder.Property(p => p.ProcessedAtUtc).HasColumnName("processed_at");
        builder.Property(p => p.ApprovedBy).HasColumnName("approved_by");
        builder.Property(p => p.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(200);

        builder.Ignore(p => p.DomainEvents);

        builder.HasMany(p => p.Receipts)
               .WithOne()
               .HasForeignKey("PaymentId");

        builder.Navigation(p => p.Receipts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PaymentReceiptConfiguration : IEntityTypeConfiguration<PaymentReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentReceipt> builder)
    {
        builder.ToTable("payment_receipt", "payment");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("payment_receipt_id").ValueGeneratedOnAdd();

        builder.Property<long>("PaymentId").HasColumnName("payment_id");
        builder.Property(r => r.FileUrl).HasColumnName("file_url").HasMaxLength(500);
        builder.Property(r => r.TransactionReference).HasColumnName("transaction_reference").HasMaxLength(100);
        builder.Property(r => r.ReportedAmount).HasColumnName("reported_amount").HasPrecision(12, 2);
        builder.Property(r => r.UploadedAtUtc).HasColumnName("uploaded_at");
        builder.Property(r => r.UploadedBy).HasColumnName("uploaded_by");
    }
}
