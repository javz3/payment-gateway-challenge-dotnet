using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Tests.Integration;

public class PaymentsControllerTests
{
    private readonly Mock<IBankClient> _bankClientMock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    private HttpClient CreateClient(IPaymentsRepository? repository = null)
    {
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        return webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankClient>();
                services.AddSingleton(_bankClientMock.Object);

                if (repository != null)
                    services.AddSingleton(repository);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new PaymentResponse
        {
            Id = Guid.NewGuid(),
            ExpiryYear = 2027,
            ExpiryMonth = 6,
            Amount = 5000,
            CardNumberLastFour = "8877",
            Currency = "GBP"
        };

        var paymentsRepository = new PaymentsRepository();
        paymentsRepository.Add(payment);

        var client = CreateClient(paymentsRepository);

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse!.Id);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_ReturnsAuthorized_WhenBankAuthorizes()
    {
        // Arrange
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "auth-123" });

        var client = CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse!.Status);
        Assert.Equal("8877", paymentResponse.CardNumberLastFour);
        Assert.Equal(12, paymentResponse.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(100, paymentResponse.Amount);
        Assert.NotEqual(Guid.Empty, paymentResponse.Id);
    }

    [Fact]
    public async Task PostPayment_ReturnsDeclined_WhenBankDeclines()
    {
        // Arrange
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false, AuthorizationCode = "" });

        var client = CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248878",
            ExpiryMonth = 6,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "USD",
            Amount = 5000,
            Cvv = "456"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse!.Status);
    }

    [Fact]
    public async Task PostPayment_Returns400_WhenRequestIsInvalid()
    {
        // Arrange
        var client = CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "123", // too short
            ExpiryMonth = 13,   // invalid month
            ExpiryYear = 2020,  // in the past
            Currency = "XYZ",   // unsupported
            Amount = -1,        // negative
            Cvv = "AB"          // non-numeric and too short
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Rejected, paymentResponse!.Status);
    }

    [Fact]
    public async Task PostThenGet_ReturnsConsistentPaymentData()
    {
        // Arrange
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = true, AuthorizationCode = "abc-123" });

        var client = CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 2,
            Currency = "EUR",
            Amount = 2500,
            Cvv = "999"
        };

        // Act — POST
        var postResponse = await client.PostAsJsonAsync("/api/Payments", request);
        var createdPayment = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);
        Assert.NotNull(createdPayment);

        // Act — GET
        var getResponse = await client.GetAsync($"/api/Payments/{createdPayment!.Id}");
        var retrievedPayment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrievedPayment);
        Assert.Equal(createdPayment.Id, retrievedPayment!.Id);
        Assert.Equal(createdPayment.Status, retrievedPayment.Status);
        Assert.Equal(createdPayment.CardNumberLastFour, retrievedPayment.CardNumberLastFour);
        Assert.Equal(createdPayment.ExpiryMonth, retrievedPayment.ExpiryMonth);
        Assert.Equal(createdPayment.ExpiryYear, retrievedPayment.ExpiryYear);
        Assert.Equal(createdPayment.Currency, retrievedPayment.Currency);
        Assert.Equal(createdPayment.Amount, retrievedPayment.Amount);
    }

    [Fact]
    public async Task PostPayment_Returns502_WhenBankIsUnavailable()
    {
        // Arrange
        _bankClientMock.Setup(b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>()))
            .ThrowsAsync(new HttpRequestException("Service Unavailable"));

        var client = CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248870",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
