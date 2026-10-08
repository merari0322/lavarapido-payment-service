using PaymentService.Domain.Common;

namespace PaymentService.Domain.Promotions;

/// <summary>
/// La promoción que se muestra como paquete (código, nombre, precio de referencia, ícono,
/// destacada, beneficios) y que además es un cupón real: al canjearla en el pago aplica
/// DiscountPercent de descuento sobre el total de la reserva (100 = gratis). discount_type_id se
/// guarda como PERCENT y discount_value como DiscountPercent (el repositorio lo llena; ver
/// PromotionRepository); min_purchase_amount/min_completed_booking existen desde antes de esta
/// pantalla y se dejan en su valor neutro.
///
/// RequiredPoints es el umbral de puntos de fidelización (payment.loyalty_transaction) que el
/// cliente necesita acumulado para que la promoción se le desbloquee; 0 significa que no depende
/// de puntos.
/// </summary>
public sealed class Promotion : Entity<int>
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
    public decimal Price { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Icon { get; private set; }
    public bool Featured { get; private set; }
    /// <summary>Una línea por beneficio; se guarda como texto, sin reglas propias.</summary>
    public IReadOnlyList<string> Benefits { get; private set; } = Array.Empty<string>();
    public DateOnly ValidFrom { get; private set; }
    public DateOnly ValidTo { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>Descuento real que aplica al canjear el cupón en el pago (1 a 100; 100 = gratis).</summary>
    public int DiscountPercent { get; private set; }
    /// <summary>Puntos de fidelización que hay que tener acumulados para que se desbloquee.</summary>
    public int RequiredPoints { get; private set; }

    public static Promotion Create(string code, string name, string? description, decimal price,
        int durationMinutes, string? icon, bool featured, IEnumerable<string> benefits,
        DateOnly validFrom, DateOnly validTo, int discountPercent, int requiredPoints)
    {
        var promotion = new Promotion();
        promotion.Update(code, name, description, price, durationMinutes, icon, featured, benefits, validFrom,
            validTo, discountPercent, requiredPoints);
        promotion.IsActive = true;
        return promotion;
    }

    public void Update(string code, string name, string? description, decimal price, int durationMinutes,
        string? icon, bool featured, IEnumerable<string> benefits, DateOnly validFrom, DateOnly validTo,
        int discountPercent, int requiredPoints)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > MaxCodeLength)
            throw new DomainException("INVALID_PROMOTION_CODE", $"El código es obligatorio y no puede superar {MaxCodeLength} caracteres.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new DomainException("INVALID_PROMOTION_NAME", $"El nombre es obligatorio y no puede superar {MaxNameLength} caracteres.");
        if (description is { Length: > MaxDescriptionLength })
            throw new DomainException("INVALID_PROMOTION_DESCRIPTION", $"La descripción no puede superar {MaxDescriptionLength} caracteres.");
        if (price <= 0)
            throw new DomainException("INVALID_PROMOTION_PRICE", "El precio debe ser mayor a cero.");
        if (durationMinutes <= 0)
            throw new DomainException("INVALID_PROMOTION_DURATION", "La duración debe ser mayor a cero.");
        if (icon is { Length: > MaxIconLength })
            throw new DomainException("INVALID_PROMOTION_ICON", $"El ícono no puede superar {MaxIconLength} caracteres.");
        if (validTo < validFrom)
            throw new DomainException("INVALID_PROMOTION_RANGE", "La fecha de fin no puede ser anterior a la de inicio.");
        if (discountPercent is < 1 or > 100)
            throw new DomainException("INVALID_PROMOTION_DISCOUNT", "El descuento debe estar entre 1 y 100.");
        if (requiredPoints < 0)
            throw new DomainException("INVALID_PROMOTION_REQUIRED_POINTS", "Los puntos requeridos no pueden ser negativos.");

        var benefitList = (benefits ?? Enumerable.Empty<string>())
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();
        var joined = string.Join('\n', benefitList);
        if (joined.Length > MaxBenefitsLength)
            throw new DomainException("INVALID_PROMOTION_BENEFITS", $"Los beneficios no pueden superar {MaxBenefitsLength} caracteres en total.");

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Price = price;
        DurationMinutes = durationMinutes;
        Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        Featured = featured;
        Benefits = benefitList;
        ValidFrom = validFrom;
        ValidTo = validTo;
        DiscountPercent = discountPercent;
        RequiredPoints = requiredPoints;
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>active / scheduled / paused, para la pantalla (se deriva, no se guarda aparte).</summary>
    public string StatusFor(DateOnly today)
    {
        if (!IsActive) return "paused";
        return ValidFrom > today ? "scheduled" : "active";
    }

    /// <summary>true si hoy está vigente (fecha y activa) y el cliente ya tiene los puntos que pide.</summary>
    public bool IsRedeemableToday(DateOnly today, int customerPoints) =>
        IsActive && today >= ValidFrom && today <= ValidTo && customerPoints >= RequiredPoints;

    /// <summary>Descuento en pesos que aplica sobre ese subtotal (100% = el subtotal completo).</summary>
    public decimal DiscountFor(decimal subtotal) =>
        Math.Round(subtotal * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
}
