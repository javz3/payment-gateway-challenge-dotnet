using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validators;

namespace PaymentGateway.Api.Tests.Unit;

public class PaymentServiceTests
{
    private readonly Mock<IBankClient> _bankClientMock = new();
    private readonly Mock<IPaymentsRepository> _repositoryMock = new();
    private readonly Mock<IPaymentValidator> _validatorMock = new();
    private readonly IPaymentService _paymentService;

    public PaymentServiceTests()
    {
        _paymentService = new PaymentService(
            _bankClientMock.Object,
            _repositoryMock.Object,
            _validatorMock.Object,
            Mock.Of<ILogger<PaymentService>>());
    }

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 12,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public async Task RejectsPayment_WhenValidationFails()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(false);

        var result = await _paymentService.ProcessPaymentAsync(request);

        Assert.Equal(PaymentStatus.Rejected, result.Status);
        _bankClientMock.Verify(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsAuthorized_WhenBankAuthorizesPayment()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });

        var result = await _paymentService.ProcessPaymentAsync(request);

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    [Fact]
    public async Task ReturnsDeclined_WhenBankDeclinesPayment()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false, AuthorizationCode = "" });

        var result = await _paymentService.ProcessPaymentAsync(request);

        Assert.Equal(PaymentStatus.Declined, result.Status);
    }

    [Fact]
    public async Task MasksCardNumber_ReturningOnlyLastFourDigits()
    {
        var request = CreateValidRequest();
        request.CardNumber = "2222405343248877";
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "abc" });

        var result = await _paymentService.ProcessPaymentAsync(request);

        Assert.Equal("8877", result.CardNumberLastFour);
    }

    [Fact]
    public async Task StoresPayment_WhenBankRespondsSuccessfully()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "abc" });

        var result = await _paymentService.ProcessPaymentAsync(request);

        _repositoryMock.Verify(r => r.Add(It.Is<PaymentResponse>(p => p.Id == result.Id)), Times.Once);
    }

    [Fact]
    public async Task DoesNotStorePayment_WhenRequestIsRejected()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(false);

        await _paymentService.ProcessPaymentAsync(request);

        _repositoryMock.Verify(r => r.Add(It.IsAny<PaymentResponse>()), Times.Never);
    }

    [Fact]
    public async Task SendsCorrectExpiryDateFormat_ToBank()
    {
        var request = CreateValidRequest();
        request.ExpiryMonth = 4;
        request.ExpiryYear = 2026;
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);

        BankPaymentRequest? capturedRequest = null;
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .Callback<BankPaymentRequest>(r => capturedRequest = r)
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "abc" });

        await _paymentService.ProcessPaymentAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal("04/2026", capturedRequest!.ExpiryDate);
        Assert.Equal(request.CardNumber, capturedRequest.CardNumber);
        Assert.Equal(request.Currency, capturedRequest.Currency);
        Assert.Equal(request.Amount, capturedRequest.Amount);
        Assert.Equal(request.Cvv, capturedRequest.Cvv);
    }

    [Fact]
    public async Task ThrowsBankUnavailableException_WhenBankFails()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ThrowsAsync(new Exceptions.BankUnavailableException("Service Unavailable"));

        await Assert.ThrowsAsync<Exceptions.BankUnavailableException>(
            () => _paymentService.ProcessPaymentAsync(request));
    }

    [Fact]
    public async Task ThrowsBankRequestException_WhenBankRejectsRequest()
    {
        var request = CreateValidRequest();
        _validatorMock.Setup(v => v.IsValid(request)).Returns(true);
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ThrowsAsync(new Exceptions.BankRequestException("Bank rejected the request with status 400."));

        await Assert.ThrowsAsync<Exceptions.BankRequestException>(
            () => _paymentService.ProcessPaymentAsync(request));
    }
}
