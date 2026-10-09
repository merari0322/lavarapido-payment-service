namespace PaymentService.Domain.Common;

/// <summary>
/// Se lanza cuando alguien intenta romper una regla del negocio (por ejemplo, rechazar un pago sin
/// indicar el motivo). La API la traduce a 400 Bad Request.
/// Code es el código estable que la web traduce (ver DomainErrorCodes); el mensaje es para el
/// desarrollador y para mostrar si la web no tiene traducción.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}
