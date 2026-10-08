namespace PaymentService.Api.Contracts;

/// <summary>Saldo de puntos del cliente.</summary>
public sealed record BalanceResponse(int Points);

/// <summary>Canjear un cupón (código de la promoción) en una reserva propia.</summary>
public sealed record RedeemPromotionRequest(long BookingId, string Code);
