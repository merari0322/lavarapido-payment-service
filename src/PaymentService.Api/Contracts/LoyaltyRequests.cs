using System.Text.Json.Serialization;

namespace PaymentService.Api.Contracts;

/// <summary>Canjear un cupón (código de la promoción) en una reserva propia; la reserva es obligatoria (BIZ-13).</summary>
public sealed record RedeemPromotionRequest([property: JsonRequired] long BookingId, string Code);
