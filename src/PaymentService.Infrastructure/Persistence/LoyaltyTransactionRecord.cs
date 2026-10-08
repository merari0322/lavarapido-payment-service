using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Una fila de payment.loyalty_transaction: el ledger de puntos de fidelización, fuente de
/// verdad del saldo (balance_after de la última fila de cada cliente). customer_id guarda el
/// mismo id de usuario (security.app_user) que booking-service expone como ownerUserId: es el
/// único identificador de cliente que cualquier servicio puede resolver hoy; el comentario de la
/// migración 014 dice "customer.customer" pero ese id interno no lo expone customer-service.
/// </summary>
public class LoyaltyTransactionRecord
{
    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public short LoyaltyMovementTypeId { get; private set; }
    public long? BookingId { get; private set; }
    public int Points { get; private set; }
    public int BalanceAfter { get; private set; }
    public string? Description { get; private set; }

    private LoyaltyTransactionRecord() { }

    public static LoyaltyTransactionRecord Create(long customerId, short movementTypeId, long? bookingId,
        int points, int balanceAfter, string? description) =>
        new()
        {
            CustomerId = customerId,
            LoyaltyMovementTypeId = movementTypeId,
            BookingId = bookingId,
            Points = points,
            BalanceAfter = balanceAfter,
            Description = description,
        };
}

public class LoyaltyTransactionRecordConfiguration : IEntityTypeConfiguration<LoyaltyTransactionRecord>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransactionRecord> builder)
    {
        builder.ToTable("loyalty_transaction", "payment");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("loyalty_transaction_id").ValueGeneratedOnAdd();
        builder.Property(l => l.CustomerId).HasColumnName("customer_id");
        builder.Property(l => l.LoyaltyMovementTypeId).HasColumnName("loyalty_movement_type_id");
        builder.Property(l => l.BookingId).HasColumnName("booking_id");
        builder.Property(l => l.Points).HasColumnName("points");
        builder.Property(l => l.BalanceAfter).HasColumnName("balance_after");
        builder.Property(l => l.Description).HasColumnName("description").HasMaxLength(200);
        builder.Property<long?>("CreatedBy").HasColumnName("created_by");
        builder.Property<DateTime?>("DeletedAt").HasColumnName("deleted_at");
        builder.HasQueryFilter(l => EF.Property<DateTime?>(l, "DeletedAt") == null);
    }
}

/// <summary>Lectura de payment.loyalty_movement_type, solo para resolver el id de EARNED/REDEEMED.</summary>
public class LoyaltyMovementTypeRecord
{
    public short Id { get; private set; }
    public string Code { get; private set; } = string.Empty;

    private LoyaltyMovementTypeRecord() { }
}

public class LoyaltyMovementTypeRecordConfiguration : IEntityTypeConfiguration<LoyaltyMovementTypeRecord>
{
    public void Configure(EntityTypeBuilder<LoyaltyMovementTypeRecord> builder)
    {
        builder.ToTable("loyalty_movement_type", "payment");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("loyalty_movement_type_id");
        builder.Property(l => l.Code).HasColumnName("code");
    }
}
