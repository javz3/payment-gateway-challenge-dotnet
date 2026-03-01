using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PaymentResponse> _payments = new();

    public void Add(PaymentResponse payment) => _payments[payment.Id] = payment;

    public PaymentResponse? Get(Guid id) =>
        _payments.TryGetValue(id, out var payment) ? payment : null;
}
