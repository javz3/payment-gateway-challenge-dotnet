namespace PaymentGateway.Api.Exceptions;

// The bank rejected our request (e.g. 400 — missing/malformed fields).
// This indicates a bug in our request mapping, not a merchant error.
// The controller surfaces this as 500 Internal Server Error.
public class BankRequestException : Exception
{
    public BankRequestException(string message) : base(message) { }
}
