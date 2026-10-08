using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Loyalty;
using PaymentService.Domain.Promotions;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Traduce los enums del dominio (DiscountType, LoyaltyMovementType) al ID real de su fila de
/// catálogo, buscándolo por código. No se puede suponer que el ID es fijo: SQL Server reserva
/// valores IDENTITY en bloques y, tras un reinicio, salta (por ejemplo, PACKAGE quedó con 102 en
/// vez de 3). Por eso los IDs se leen una vez al arrancar (LoadAsync) y los convertidores de EF
/// (ver Configurations/) consultan este mapa en cada lectura y escritura.
///
/// Es estático porque los ValueConverter de EF se definen una sola vez al construir el modelo; el
/// mapa en sí solo cambia en LoadAsync y se reemplaza completo (sin estados intermedios).
/// payment_status no pasa por aquí: su ID 3 = APPROVED está fijado en un índice de la base.
/// </summary>
internal static class CatalogIds
{
    private static Map<DiscountType> _discountTypes = Map<DiscountType>.Empty("promotion.discount_type");
    private static Map<LoyaltyMovementType> _movementTypes = Map<LoyaltyMovementType>.Empty("payment.loyalty_movement_type");

    public static short ToId(DiscountType type) => _discountTypes.ToId(type);
    public static DiscountType ToDiscountType(short id) => _discountTypes.FromId(id);
    public static short ToId(LoyaltyMovementType type) => _movementTypes.ToId(type);
    public static LoyaltyMovementType ToMovementType(short id) => _movementTypes.FromId(id);

    /// <summary>Lee ambos catálogos de la base y reemplaza los mapas.</summary>
    public static async Task LoadAsync(PaymentDbContext db, CancellationToken ct)
    {
        var discountRows = await ReadAsync(db, "SELECT discount_type_id AS Id, code AS Code FROM promotion.discount_type", ct);
        var movementRows = await ReadAsync(db, "SELECT loyalty_movement_type_id AS Id, code AS Code FROM payment.loyalty_movement_type", ct);

        _discountTypes = Map<DiscountType>.Build("promotion.discount_type", discountRows, Enum.GetValues<DiscountType>(), t => t.ToCode());
        _movementTypes = Map<LoyaltyMovementType>.Build("payment.loyalty_movement_type", movementRows,
            Enum.GetValues<LoyaltyMovementType>(), t => t.ToCode());
    }

    private static Task<List<CatalogRow>> ReadAsync(PaymentDbContext db, string sql, CancellationToken ct) =>
        db.Database.SqlQueryRaw<CatalogRow>(sql).ToListAsync(ct);

    // Fila genérica de catálogo (EF 8 permite consultar tipos no mapeados con SqlQueryRaw).
    private sealed record CatalogRow(short Id, string Code);

    /// <summary>Mapa bidireccional enum ↔ ID de un catálogo.</summary>
    private sealed class Map<TEnum> where TEnum : struct, Enum
    {
        private readonly string _table;
        private readonly IReadOnlyDictionary<TEnum, short> _ids;
        private readonly IReadOnlyDictionary<short, TEnum> _values;

        private Map(string table, IReadOnlyDictionary<TEnum, short> ids)
        {
            _table = table;
            _ids = ids;
            _values = ids.ToDictionary(p => p.Value, p => p.Key);
        }

        public static Map<TEnum> Empty(string table) => new(table, new Dictionary<TEnum, short>());

        // Un código del enum que falte en la tabla no rompe el arranque: solo falla si se usa
        // (por ejemplo, PACKAGE antes de aplicar la migración 017).
        public static Map<TEnum> Build(string table, IEnumerable<CatalogRow> rows, IEnumerable<TEnum> values,
            Func<TEnum, string> codeOf)
        {
            var idByCode = rows.ToDictionary(r => r.Code.Trim().ToUpperInvariant(), r => r.Id);
            var ids = values
                .Where(v => idByCode.ContainsKey(codeOf(v)))
                .ToDictionary(v => v, v => idByCode[codeOf(v)]);
            return new Map<TEnum>(table, ids);
        }

        public short ToId(TEnum value) =>
            _ids.TryGetValue(value, out var id)
                ? id
                : throw new InvalidOperationException($"{_table} has no row for {value} (catalog not loaded or not seeded).");

        public TEnum FromId(short id) =>
            _values.TryGetValue(id, out var value)
                ? value
                : throw new InvalidOperationException($"{_table} id {id} is not a known code (catalog not loaded or not seeded).");
    }
}
