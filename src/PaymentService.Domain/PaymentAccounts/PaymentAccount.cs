using PaymentService.Domain.Common;

namespace PaymentService.Domain.PaymentAccounts;

/// <summary>
/// Cuenta del lavadero a la que el cliente paga (tabla payment.payment_account): una por medio de
/// pago, con el QR que el cliente escanea desde la app de su banco. El efectivo también tiene su
/// fila (sin QR) para que todo pago apunte siempre a alguna cuenta, sin casos especiales.
/// </summary>
public sealed class PaymentAccount : Entity<short>
{
    private const int MaxHolderLength = 120;
    private const int MaxNumberLength = 50;
    private const int MaxInstructionsLength = 300;

    private PaymentAccount() { }

    public short PaymentMethodTypeId { get; private set; }
    public string AccountHolder { get; private set; } = string.Empty;
    public string? AccountNumber { get; private set; }

    /// <summary>Imagen del QR (data URL o http); null para el efectivo.</summary>
    public string? QrImageUrl { get; private set; }

    /// <summary>Texto que se muestra junto al QR.</summary>
    public string? Instructions { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Factory Method: crea la cuenta reutilizando las mismas validaciones de Update.</summary>
    public static PaymentAccount Create(PaymentMethodType method, string holder, string? number,
        string? qrImageUrl, string? instructions, bool active)
    {
        var account = new PaymentAccount();
        account.Update(method, holder, number, qrImageUrl, instructions, active);
        return account;
    }

    /// <summary>Reemplaza los datos de la cuenta validando longitudes y que el QR sea una imagen.</summary>
    public void Update(PaymentMethodType method, string holder, string? number, string? qrImageUrl,
        string? instructions, bool active)
    {
        ArgumentNullException.ThrowIfNull(method);
        var qr = string.IsNullOrWhiteSpace(qrImageUrl) ? null : qrImageUrl;
        Guard.Against(qr is not null && !ImageSource.IsImage(qr), "INVALID_QR_IMAGE", "El QR debe ser una imagen.");

        PaymentMethodTypeId = method.Id;
        AccountHolder = Guard.Required(holder, MaxHolderLength, "INVALID_ACCOUNT_HOLDER",
            $"El titular es obligatorio y no puede superar {MaxHolderLength} caracteres.");
        AccountNumber = Guard.Optional(number, MaxNumberLength, "INVALID_ACCOUNT_NUMBER",
            $"El número de cuenta no puede superar {MaxNumberLength} caracteres.");
        QrImageUrl = qr;
        Instructions = Guard.Optional(instructions, MaxInstructionsLength, "INVALID_ACCOUNT_INSTRUCTIONS",
            $"Las instrucciones no pueden superar {MaxInstructionsLength} caracteres.");
        IsActive = active;
    }
}
