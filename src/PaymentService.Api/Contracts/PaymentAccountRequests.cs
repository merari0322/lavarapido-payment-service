namespace PaymentService.Api.Contracts;

/// <summary>Crear o editar una cuenta del lavadero; el medio se indica por su código (NEQUI, EFECTIVO...).</summary>
public sealed record SaveAccountRequest(string MethodCode, string AccountHolder, string? AccountNumber,
    string? QrImageUrl, string? Instructions, bool Active);
