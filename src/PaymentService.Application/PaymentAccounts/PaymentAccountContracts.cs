namespace PaymentService.Application.PaymentAccounts;

/// <summary>Cuenta del lavadero con los datos de su medio de pago, como la ven la web y la app.</summary>
public sealed record PaymentAccountDto(
    short Id,
    string MethodCode,
    string MethodName,
    string AccountHolder,
    string? AccountNumber,
    string? QrImageUrl,
    string? Instructions,
    bool Active,
    bool RequiresReceipt);

/// <summary>Medio de pago del catálogo.</summary>
public sealed record PaymentMethodDto(short Id, string Code, string Name, bool RequiresReceipt);

/// <summary>Datos para crear o editar una cuenta; el medio se indica por su código (NEQUI, EFECTIVO...).</summary>
public sealed record SaveAccountCommand(
    string MethodCode,
    string AccountHolder,
    string? AccountNumber,
    string? QrImageUrl,
    string? Instructions,
    bool Active);
