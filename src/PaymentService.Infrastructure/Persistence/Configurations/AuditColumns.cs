using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Bloque de auditoría estándar de todas las tablas (modeling-conventions.md): updated_at,
/// deleted_at, deleted_by y row_version como propiedades "shadow" de EF (existen en la tabla pero
/// no en el dominio, porque son detalles de persistencia), más el filtro global que oculta las
/// filas con borrado lógico. created_at lo pone la base por defecto (SYSUTCDATETIME()).
///
/// row_version es el token de concurrencia optimista: EfUnitOfWork lo incrementa en cada UPDATE y
/// EF agrega "AND row_version = &lt;el leído&gt;" al WHERE, así dos requests que modifican la misma
/// fila a la vez no se pisan (el segundo recibe 409).
/// Se declara una sola vez aquí y cada configuración lo aplica con HasAuditColumns().
/// </summary>
internal static class AuditColumns
{
    public const string UpdatedAt = nameof(UpdatedAt);
    public const string DeletedAt = nameof(DeletedAt);
    public const string DeletedBy = nameof(DeletedBy);
    public const string RowVersion = nameof(RowVersion);

    public static EntityTypeBuilder<T> HasAuditColumns<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<DateTime?>(UpdatedAt).HasColumnName("updated_at");
        builder.Property<DateTime?>(DeletedAt).HasColumnName("deleted_at");
        builder.Property<long?>(DeletedBy).HasColumnName("deleted_by");
        builder.Property<int>(RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasQueryFilter(e => EF.Property<DateTime?>(e, DeletedAt) == null);
        return builder;
    }
}
