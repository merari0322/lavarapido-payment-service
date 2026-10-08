namespace PaymentService.Domain.Common;

/// <summary>
/// Regla única para saber si un texto es una imagen que la web puede mostrar: mientras no haya
/// almacenamiento de archivos (migración 016) el QR y el comprobante viajan como data URL, o como
/// URL http(s) si algún día se suben a un almacenamiento externo.
/// </summary>
public static class ImageSource
{
    public static bool IsImage(string? value) =>
        value is not null &&
        (value.StartsWith("data:image/", StringComparison.Ordinal) ||
         value.StartsWith("http", StringComparison.Ordinal));
}
