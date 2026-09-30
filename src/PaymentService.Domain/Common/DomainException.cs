namespace PaymentService.Domain.Common;

/// <summary>
/// Se lanza cuando alguien intenta romper una regla del negocio
/// (ej. rechazar un pago sin indicar el motivo).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
