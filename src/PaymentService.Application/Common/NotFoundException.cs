namespace PaymentService.Application.Common;

public class NotFoundException : Exception
{
    public string Code { get; }

    public NotFoundException(string message) : this("NOT_FOUND", message) { }

    public NotFoundException(string code, string message) : base(message)
    {
        Code = code;
    }
}
