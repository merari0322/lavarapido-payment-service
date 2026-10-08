namespace PaymentService.Domain.Common;

/// <summary>
/// Validaciones repetidas en todas las entidades (texto obligatorio, longitud máxima, valores
/// positivos). Centralizarlas evita copiar el mismo if/throw en cada aggregate y garantiza que
/// todos normalicen el texto de la misma forma (recortado; vacío pasa a null).
/// </summary>
internal static class Guard
{
    /// <summary>Lanza DomainException con ese código si la condición se cumple.</summary>
    public static void Against(bool condition, string code, string message)
    {
        if (condition) throw new DomainException(code, message);
    }

    /// <summary>Texto obligatorio: no vacío y dentro del máximo. Devuelve el valor recortado.</summary>
    public static string Required(string? value, int maxLength, string code, string message)
    {
        var trimmed = value?.Trim();
        Against(string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength, code, message);
        return trimmed!;
    }

    /// <summary>Texto opcional: si viene, dentro del máximo. Devuelve el valor recortado o null.</summary>
    public static string? Optional(string? value, int maxLength, string code, string message)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        Against(trimmed.Length > maxLength, code, message);
        return trimmed;
    }

    /// <summary>Identificador de otra tabla o servicio: siempre mayor que cero.</summary>
    public static void PositiveId(long id, string code, string message) => Against(id <= 0, code, message);
}
