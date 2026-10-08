using PaymentService.Domain.Common;
using PaymentService.Domain.Promotions.Discounts;

namespace PaymentService.Domain.Promotions;

/// <summary>
/// Aggregate root de una promoción (tabla promotion.promotion). Se muestra como paquete (precio de
/// referencia, duración, ícono, beneficios) y es también un cupón real: al canjearla en el pago
/// descuenta según su tipo (Strategy, ver Discounts/) sobre lo que falta por pagar de la reserva.
///
/// Reglas del canje (Redeem): vigencia y activa, puntos de fidelización suficientes (RequiredPoints),
/// compra mínima, una sola vez por reserva y los límites de usos total y por cliente. El número de
/// usos no se guarda aquí: se deriva contando promotion.booking_promotion, para no tener un segundo
/// contador que se desincronice.
///
/// Columnas que no se modelan a propósito: is_public, min_completed_booking y las tablas
/// promotion_customer / promotion_service. Evaluarlas requiere datos de customer-service y
/// booking-service que hoy no se exponen; las filas conservan sus valores por defecto (pública,
/// 0 reservas, sin restricciones), que es exactamente el comportamiento actual.
/// </summary>
public sealed class Promotion : AggregateRoot<int>
{
    private const int MaxCodeLength = 30;
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 300;
    private const int MaxIconLength = 40;
    private const int MaxBenefitsLength = 600;

    private Promotion() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Precio de referencia del paquete que se muestra en la pantalla de promociones.</summary>
    public decimal Price { get; private set; }

    public int DurationMinutes { get; private set; }
    public string? Icon { get; private set; }
    public bool Featured { get; private set; }

    // La columna benefits es NULL cuando no hay beneficios, y EF no pasa los NULL por el conversor:
    // por eso se persiste este campo nullable y la propiedad pública nunca devuelve null.
    private IReadOnlyList<string>? _benefits;

    /// <summary>Una línea por beneficio; es solo texto para mostrar.</summary>
    public IReadOnlyList<string> Benefits
    {
        get => _benefits ?? Array.Empty<string>();
        private set => _benefits = value.Count == 0 ? null : value;
    }

    public DateOnly ValidFrom { get; private set; }
    public DateOnly ValidTo { get; private set; }
    public bool IsActive { get; private set; }

    public DiscountType DiscountType { get; private set; }

    /// <summary>Porcentaje o monto en pesos, según DiscountType.</summary>
    public decimal DiscountValue { get; private set; }

    /// <summary>Tope en pesos de un descuento porcentual (opcional).</summary>
    public decimal? MaxDiscountAmount { get; private set; }

    /// <summary>Compra mínima para poder canjearla (0 = sin mínimo).</summary>
    public decimal MinPurchaseAmount { get; private set; }

    /// <summary>Usos totales permitidos (null = sin límite).</summary>
    public int? MaxRedemptions { get; private set; }

    /// <summary>Usos permitidos por cliente (null = sin límite).</summary>
    public int? MaxRedemptionsPerCustomer { get; private set; }

    /// <summary>Puntos acumulados que el cliente necesita para desbloquearla (0 = no depende de puntos).</summary>
    public int RequiredPoints { get; private set; }

    /// <summary>
    /// El porcentaje como entero, para la web y la app (contrato previo de la API). Para descuentos
    /// que no son porcentuales vale 0; el valor real está en DiscountValue.
    /// </summary>
    public int DiscountPercent =>
        DiscountType == DiscountType.Percentage ? (int)Math.Round(DiscountValue, MidpointRounding.AwayFromZero) : 0;

    // ------------------------------------------------------------------ ciclo de vida

    /// <summary>Factory Method: una promoción nueva nace activa y sin límites de uso.</summary>
    public static Promotion Create(PromotionDefinition definition)
    {
        var promotion = new Promotion { IsActive = true };
        promotion.Apply(definition);
        return promotion;
    }

    /// <summary>Reemplaza lo que el admin define; los límites de uso y la compra mínima se conservan.</summary>
    public void Update(PromotionDefinition definition) => Apply(definition);

    /// <summary>Pausa o reanuda la promoción sin borrarla.</summary>
    public void SetActive(bool active) => IsActive = active;

    /// <summary>active / scheduled / paused para la pantalla del admin (se deriva, no se guarda).</summary>
    public string StatusFor(DateOnly today)
    {
        if (!IsActive) return "paused";
        return ValidFrom > today ? "scheduled" : "active";
    }

    /// <summary>Activa y dentro de su rango de fechas.</summary>
    public bool IsAvailableOn(DateOnly today) => IsActive && today >= ValidFrom && today <= ValidTo;

    /// <summary>El cliente ya tiene los puntos que pide.</summary>
    public bool IsUnlockedFor(int customerPoints) => customerPoints >= RequiredPoints;

    // ------------------------------------------------------------------ canje

    /// <summary>
    /// Descuento en pesos sobre ese subtotal: lo calcula la estrategia del tipo y luego se aplican
    /// los topes (MaxDiscountAmount y nunca más que el propio subtotal), redondeado a centavos.
    /// </summary>
    public decimal CalculateDiscount(decimal subtotal)
    {
        if (subtotal <= 0) return 0m;
        var discount = DiscountStrategyFactory.For(DiscountType).Calculate(subtotal, DiscountValue);
        if (MaxDiscountAmount is { } cap) discount = Math.Min(discount, cap);
        discount = Math.Min(discount, subtotal);
        return Math.Round(discount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Valida todas las reglas del canje y, si se cumplen, devuelve el registro del canje con el
    /// monto congelado. Cada regla tiene su propio mensaje para que el cliente sepa por qué no pudo.
    /// </summary>
    public PromotionRedemption Redeem(RedemptionRequest request)
    {
        Guard.Against(!IsAvailableOn(request.Today), "PROMOTION_NOT_REDEEMABLE", "Ese cupón no está vigente.");
        Guard.Against(!IsUnlockedFor(request.CustomerPoints), "PROMOTION_NOT_REDEEMABLE",
            "Todavía no acumulas los puntos necesarios para desbloquear ese cupón.");
        Guard.Against(request.AlreadyRedeemedOnBooking, "PROMOTION_ALREADY_REDEEMED", "Ya canjeaste este cupón en esta reserva.");
        Guard.Against(request.Subtotal < MinPurchaseAmount, "PROMOTION_MIN_PURCHASE_NOT_MET",
            $"Este cupón exige una compra mínima de {MinPurchaseAmount:N0}.");
        Guard.Against(MaxRedemptions is { } total && request.TotalRedemptions >= total, "PROMOTION_EXHAUSTED",
            "Este cupón ya alcanzó su número máximo de usos.");
        Guard.Against(MaxRedemptionsPerCustomer is { } perCustomer && request.CustomerRedemptions >= perCustomer,
            "PROMOTION_CUSTOMER_LIMIT_REACHED", "Ya usaste este cupón el máximo de veces permitido.");

        return PromotionRedemption.Create(request.BookingId, Id, CalculateDiscount(request.Subtotal), request.CustomerUserId);
    }

    // ------------------------------------------------------------------ validación

    private void Apply(PromotionDefinition d)
    {
        Guard.Against(d.Price <= 0, "INVALID_PROMOTION_PRICE", "El precio debe ser mayor a cero.");
        Guard.Against(d.DurationMinutes <= 0, "INVALID_PROMOTION_DURATION", "La duración debe ser mayor a cero.");
        Guard.Against(d.ValidTo < d.ValidFrom, "INVALID_PROMOTION_RANGE", "La fecha de fin no puede ser anterior a la de inicio.");
        Guard.Against(d.RequiredPoints < 0, "INVALID_PROMOTION_REQUIRED_POINTS", "Los puntos requeridos no pueden ser negativos.");
        // La estrategia del tipo sabe qué valores son válidos para ella (y rechaza PACKAGE).
        DiscountStrategyFactory.For(d.DiscountType).Validate(d.DiscountValue);

        var benefits = (d.Benefits ?? Array.Empty<string>())
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();
        Guard.Against(string.Join('\n', benefits).Length > MaxBenefitsLength, "INVALID_PROMOTION_BENEFITS",
            $"Los beneficios no pueden superar {MaxBenefitsLength} caracteres en total.");

        Code = Guard.Required(d.Code, MaxCodeLength, "INVALID_PROMOTION_CODE",
            $"El código es obligatorio y no puede superar {MaxCodeLength} caracteres.").ToUpperInvariant();
        Name = Guard.Required(d.Name, MaxNameLength, "INVALID_PROMOTION_NAME",
            $"El nombre es obligatorio y no puede superar {MaxNameLength} caracteres.");
        Description = Guard.Optional(d.Description, MaxDescriptionLength, "INVALID_PROMOTION_DESCRIPTION",
            $"La descripción no puede superar {MaxDescriptionLength} caracteres.");
        Icon = Guard.Optional(d.Icon, MaxIconLength, "INVALID_PROMOTION_ICON",
            $"El ícono no puede superar {MaxIconLength} caracteres.");
        Price = d.Price;
        DurationMinutes = d.DurationMinutes;
        Featured = d.Featured;
        Benefits = benefits;
        ValidFrom = d.ValidFrom;
        ValidTo = d.ValidTo;
        DiscountType = d.DiscountType;
        DiscountValue = d.DiscountValue;
        RequiredPoints = d.RequiredPoints;
    }
}
