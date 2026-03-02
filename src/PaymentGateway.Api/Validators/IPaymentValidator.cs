using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validators;

public interface IPaymentValidator
{
    bool IsValid(PostPaymentRequest request);
}
