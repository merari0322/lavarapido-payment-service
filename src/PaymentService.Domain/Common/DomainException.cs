namespace PaymentService.Domain.Common;

/// <summary>
/// Se lanza cuando alguien intenta romper una regla del negocio
/// (ej. rechazar un pago sin indicar el motivo).
/// Code es el código estable que la web traduce (API_ERRORS.&lt;code&gt;).
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message) : this("PAYMENT_RULE_VIOLATED", message) { }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}
