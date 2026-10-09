namespace PaymentService.Domain.Promotions;

/// <summary>
/// Todo lo que Promotion necesita saber para decidir si se puede canjear en una reserva. Lo arma
/// la capa de aplicación consultando el ledger de puntos y los canjes previos; así la regla de
/// negocio queda en el dominio sin que el dominio dependa de repositorios.
/// </summary>
/// <param name="BookingId">Reserva sobre la que se canjea.</param>
/// <param name="BookingCode">Código visible de la reserva (viaja en el evento del canje).</param>
/// <param name="CustomerUserId">Cliente que canjea (id de security.app_user).</param>
/// <param name="NowUtc">Momento del canje; su fecha decide la vigencia.</param>
/// <param name="CustomerPoints">Saldo actual de puntos del cliente (ledger).</param>
/// <param name="Subtotal">Lo que falta por pagar de la reserva antes de este canje.</param>
/// <param name="AlreadyRedeemedOnBooking">Si esta promoción ya se canjeó en esta reserva.</param>
/// <param name="TotalRedemptions">Cuántas veces se ha canjeado en total.</param>
/// <param name="CustomerRedemptions">Cuántas veces la ha canjeado este cliente.</param>
public sealed record RedemptionRequest(
    long BookingId,
    string BookingCode,
    long CustomerUserId,
    DateTime NowUtc,
    int CustomerPoints,
    decimal Subtotal,
    bool AlreadyRedeemedOnBooking,
    int TotalRedemptions,
    int CustomerRedemptions)
{
    /// <summary>Fecha del canje, para comparar con la vigencia de la promoción.</summary>
    public DateOnly Today => DateOnly.FromDateTime(NowUtc);
}
