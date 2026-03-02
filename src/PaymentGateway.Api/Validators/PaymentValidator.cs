using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validators;

// Validation is explicit rather than using data annotations or FluentValidation.
// The cross-field expiry check (month + year) doesn't fit neatly into attribute-based validation,
// and a dedicated class keeps validation independently testable without adding a dependency.
public class PaymentValidator : IPaymentValidator
{
    // Hardcoded for the exercise. In production, supported currencies would be
    // per-merchant configuration — each merchant is enabled for specific currencies
    // based on their acquiring bank, payment methods, and settlement setup.
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP", "USD", "EUR"
    };

    public bool IsValid(PostPaymentRequest request)
    {
        if (string.IsNullOrEmpty(request.CardNumber)
            || request.CardNumber.Length < 14
            || request.CardNumber.Length > 19
            || !request.CardNumber.All(char.IsDigit))
        {
            return false;
        }

        if (request.ExpiryMonth < 1 || request.ExpiryMonth > 12)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (request.ExpiryYear < now.Year
            || (request.ExpiryYear == now.Year && request.ExpiryMonth < now.Month))
        {
            return false;
        }

        if (string.IsNullOrEmpty(request.Currency)
            || !SupportedCurrencies.Contains(request.Currency))
        {
            return false;
        }

        if (request.Amount <= 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(request.Cvv)
            || request.Cvv.Length < 3
            || request.Cvv.Length > 4
            || !request.Cvv.All(char.IsDigit))
        {
            return false;
        }

        return true;
    }
}
