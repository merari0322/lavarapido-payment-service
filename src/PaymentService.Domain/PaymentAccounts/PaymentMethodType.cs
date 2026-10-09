using PaymentService.Domain.Common;

namespace PaymentService.Domain.PaymentAccounts;

/// <summary>
/// Medio de pago del catálogo (EFECTIVO, NEQUI, DAVIPLATA,
/// TRANSFERENCIA). Es de solo lectura para este servicio: los medios vienen predefinidos.
/// </summary>
public sealed class PaymentMethodType : Entity<short>
{
    private PaymentMethodType() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    /// <summary>Si el medio necesita una cuenta registrada (todos menos el efectivo).</summary>
    public bool RequiresAccount { get; private set; }

    /// <summary>
    /// Si el cliente debe subir comprobante. Solo los medios que lo exigen (los de QR) se pueden
    /// reportar desde la app; el efectivo lo registra el admin en persona.
    /// </summary>
    public bool RequiresReceipt { get; private set; }

    public short DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
}
