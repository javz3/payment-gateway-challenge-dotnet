namespace PaymentGateway.Api.Exceptions;

// Transient/infrastructure failure — the bank is down, timed out, or unreachable.
// The controller surfaces this as 502 Bad Gateway.
public class BankUnavailableException : Exception
{
    public BankUnavailableException(string message) : base(message) { }
    public BankUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
