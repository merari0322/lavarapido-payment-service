using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>Lectura de promotion.discount_type, solo para resolver el id de PACKAGE al guardar.</summary>
public class DiscountTypeRecord
{
    public short Id { get; private set; }
    public string Code { get; private set; } = string.Empty;

    private DiscountTypeRecord() { }
}

public class DiscountTypeRecordConfiguration : IEntityTypeConfiguration<DiscountTypeRecord>
{
    public void Configure(EntityTypeBuilder<DiscountTypeRecord> builder)
    {
        builder.ToTable("discount_type", "promotion");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("discount_type_id");
        builder.Property(d => d.Code).HasColumnName("code");
    }
}
