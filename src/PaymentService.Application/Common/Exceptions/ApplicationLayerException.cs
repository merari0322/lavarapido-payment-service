namespace PaymentService.Application.Common.Exceptions;

/// <summary>
/// Base de los errores que detecta la capa de aplicación (no son reglas de un aggregate, sino
/// situaciones del caso de uso: algo no existe, choca con datos ya guardados o un servicio externo
/// no responde). Cada subclase representa un tipo de error; la API decide el código HTTP por el
/// tipo, sin depender de listas de códigos de texto.
/// </summary>
public abstract class ApplicationLayerException : Exception
{
    /// <summary>Código estable que la web traduce (ver ErrorCodes).</summary>
    public string Code { get; }

    protected ApplicationLayerException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>Lo que se pidió no existe (o el que llama no tiene permiso de verlo). HTTP 404.</summary>
public sealed class NotFoundException : ApplicationLayerException
{
    public NotFoundException(string code, string message) : base(code, message) { }
}

/// <summary>La operación choca con el estado actual de los datos (ya aprobado, código repetido...). HTTP 409.</summary>
public sealed class ConflictException : ApplicationLayerException
{
    public ConflictException(string code, string message) : base(code, message) { }
}

/// <summary>Un servicio del que dependemos (booking-service) no respondió. HTTP 503.</summary>
public sealed class ServiceUnavailableException : ApplicationLayerException
{
    public ServiceUnavailableException(string code, string message) : base(code, message) { }
}
