using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Validators;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly IBankClient _bankClient;
    private readonly IPaymentsRepository _repository;
    private readonly IPaymentValidator _validator;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IBankClient bankClient,
        IPaymentsRepository repository,
        IPaymentValidator validator,
        ILogger<PaymentService> logger)
    {
        _bankClient = bankClient;
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public PaymentResponse? GetPayment(Guid id) => _repository.Get(id);

    public async Task<PaymentResponse> ProcessPaymentAsync(PostPaymentRequest request)
    {
        if (!_validator.IsValid(request))
        {
            _logger.LogWarning("Payment rejected: request failed validation");
            return new PaymentResponse
            {
                Id = Guid.NewGuid(),
                Status = PaymentStatus.Rejected
            };
        }

        BankPaymentRequest bankRequest = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        BankPaymentResponse bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        PaymentResponse payment = new PaymentResponse
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

        _logger.LogInformation(
            "Payment {PaymentId} processed: {Status}, card ending {LastFour}, {Amount} {Currency}",
            payment.Id, payment.Status, payment.CardNumberLastFour, payment.Amount, payment.Currency);

        return payment;
    }
}
