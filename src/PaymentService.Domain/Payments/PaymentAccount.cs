using PaymentService.Domain.Common;

namespace PaymentService.Domain.Payments;

/// <summary>
/// Cuenta del lavadero a la que el cliente paga (Nequi, Daviplata, transferencia...).
/// Guarda el QR que el cliente escanea con la app de su banco.
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
    public string? QrImageUrl { get; private set; }
    public string? Instructions { get; private set; }
    public bool IsActive { get; private set; }

    public static PaymentAccount Create(short methodTypeId, string holder, string? number, string? qrImageUrl,
        string? instructions, bool active)
    {
        var account = new PaymentAccount();
        account.Update(methodTypeId, holder, number, qrImageUrl, instructions, active);
        return account;
    }

    public void Update(short methodTypeId, string holder, string? number, string? qrImageUrl,
        string? instructions, bool active)
    {
        if (methodTypeId <= 0)
            throw new DomainException("La cuenta debe indicar el medio de pago.");
        if (string.IsNullOrWhiteSpace(holder) || holder.Trim().Length > MaxHolderLength)
            throw new DomainException($"El titular es obligatorio y no puede superar {MaxHolderLength} caracteres.");
        if (number is { Length: > MaxNumberLength })
            throw new DomainException($"El número de cuenta no puede superar {MaxNumberLength} caracteres.");
        if (instructions is { Length: > MaxInstructionsLength })
            throw new DomainException($"Las instrucciones no pueden superar {MaxInstructionsLength} caracteres.");
        if (qrImageUrl is not null && qrImageUrl.Length > 0 && !qrImageUrl.StartsWith("data:image/") && !qrImageUrl.StartsWith("http"))
            throw new DomainException("El QR debe ser una imagen.");

        PaymentMethodTypeId = methodTypeId;
        AccountHolder = holder.Trim();
        AccountNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        QrImageUrl = string.IsNullOrWhiteSpace(qrImageUrl) ? null : qrImageUrl;
        Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        IsActive = active;
    }
}

/// <summary>Medio de pago del catálogo (EFECTIVO, NEQUI, DAVIPLATA, TRANSFERENCIA).</summary>
public sealed class PaymentMethodType : Entity<short>
{
    private PaymentMethodType() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool RequiresReceipt { get; private set; }
}
