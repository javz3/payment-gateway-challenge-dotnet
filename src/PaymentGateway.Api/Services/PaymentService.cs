using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP", "USD", "EUR"
    };

    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _repository;

    public PaymentService(IBankClient bankClient, IPaymentsRepository repository)
    {
        _bankClient = bankClient;
        _repository = repository;
    }

    public PaymentResponse? GetPayment(Guid id) => _repository.Get(id);

    public async Task<PaymentResponse> ProcessPaymentAsync(PostPaymentRequest request)
    {
        if (!IsValid(request))
        {
            return new PaymentResponse
            {
                Id = Guid.NewGuid(),
                Status = PaymentStatus.Rejected
            };
        }

        var bankRequest = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        var payment = new PaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        _repository.Add(payment);

        return payment;
    }

    private static bool IsValid(PostPaymentRequest request)
    {
        if (string.IsNullOrEmpty(request.CardNumber)
            || request.CardNumber.Length < 14
            || request.CardNumber.Length > 19
            || !request.CardNumber.All(char.IsDigit))
            return false;

        if (request.ExpiryMonth < 1 || request.ExpiryMonth > 12)
            return false;

        var now = DateTime.UtcNow;
        if (request.ExpiryYear < now.Year
            || (request.ExpiryYear == now.Year && request.ExpiryMonth < now.Month))
            return false;

        if (string.IsNullOrEmpty(request.Currency)
            || !SupportedCurrencies.Contains(request.Currency))
            return false;

        if (request.Amount <= 0)
            return false;

        if (string.IsNullOrEmpty(request.Cvv)
            || request.Cvv.Length < 3
            || request.Cvv.Length > 4
            || !request.Cvv.All(char.IsDigit))
            return false;

        return true;
    }
}
