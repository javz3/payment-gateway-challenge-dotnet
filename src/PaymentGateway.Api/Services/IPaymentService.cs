using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    PaymentResponse? GetPayment(Guid id);
    Task<PaymentResponse> ProcessPaymentAsync(PostPaymentRequest request);
}
