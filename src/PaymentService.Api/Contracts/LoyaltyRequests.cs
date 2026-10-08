namespace PaymentService.Api.Contracts;

/// <summary>Canjear un cupón (código de la promoción) en una reserva propia.</summary>
public sealed record RedeemPromotionRequest(long BookingId, string Code);
