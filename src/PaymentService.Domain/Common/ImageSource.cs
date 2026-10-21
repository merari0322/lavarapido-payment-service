namespace PaymentService.Domain.Common;

/// <summary>
/// Regla única para saber si un texto es una imagen que la web puede mostrar. Mientras no haya
/// almacenamiento de archivos el QR y el comprobante viajan como data URL en base64.
///
/// Solo se aceptan formatos raster comunes: SVG queda fuera porque puede llevar scripts, y las URL
/// http(s) también, porque harían que el panel del admin cargue lo que el cliente quiera desde
/// fuera. Si algún día se suben a un almacenamiento propio, aquí se agrega su dominio.
/// </summary>
public static class ImageSource
{
    private static readonly string[] AllowedPrefixes =
    {
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/webp;base64,",
        "data:image/gif;base64,"
    };

    public static bool IsImage(string? value) =>
        value is not null &&
        AllowedPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.Length > prefix.Length);

    /// <summary>
    /// Bytes que ocupa la imagen ya decodificada (sin el prefijo data: y sin el relleno '=' del
    /// base64). Sirve para limitar el tamaño del archivo sin tener que decodificarlo.
    /// </summary>
    public static long DecodedSize(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        var payload = comma < 0 ? dataUrl.AsSpan() : dataUrl.AsSpan(comma + 1);
        var padding = payload.EndsWith("==") ? 2 : payload.EndsWith("=") ? 1 : 0;
        return payload.Length / 4L * 3 - padding;
    }
}
