using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories;

public interface IPaymentsRepository
{
    void Add(PaymentResponse payment);
    PaymentResponse? Get(Guid id);
}
