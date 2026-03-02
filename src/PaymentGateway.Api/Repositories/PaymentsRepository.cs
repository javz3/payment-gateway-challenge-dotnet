using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories;

// In-memory storage as specified by the requirements. ConcurrentDictionary provides
// thread safety for the Singleton lifetime and O(1) lookup by GUID.
// In production: persistent storage (SQL/Redis) with the repository registered as Scoped.
public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PaymentResponse> _payments = new();

    public void Add(PaymentResponse payment) => _payments[payment.Id] = payment;

    public PaymentResponse? Get(Guid id) =>
        _payments.TryGetValue(id, out PaymentResponse? payment) ? payment : null;
}
