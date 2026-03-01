using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Clients;

public interface IBankClient
{
    Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request);
}
