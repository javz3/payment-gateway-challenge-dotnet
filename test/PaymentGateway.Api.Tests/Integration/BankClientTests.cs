using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Clients;

namespace PaymentGateway.Api.Tests.Integration;

/// <summary>
/// Integration tests against the bank simulator. Requires: docker-compose up
/// </summary>
public class BankClientTests
{
    private readonly BankClient _sut;

    public BankClientTests()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
        _sut = new BankClient(httpClient);
    }

    private static BankPaymentRequest CreateRequest(string cardNumber) => new()
    {
        CardNumber = cardNumber,
        ExpiryDate = "12/2027",
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public async Task ReturnsAuthorized_WhenLastDigitIsOdd()
    {
        // Arrange
        var request = CreateRequest("2222405343248877");

        // Act
        var result = await _sut.ProcessPaymentAsync(request);

        // Assert
        Assert.True(result.Authorized);
        Assert.NotEmpty(result.AuthorizationCode);
    }

    [Fact]
    public async Task ReturnsDeclined_WhenLastDigitIsEven()
    {
        // Arrange
        var request = CreateRequest("2222405343248878");

        // Act
        var result = await _sut.ProcessPaymentAsync(request);

        // Assert
        Assert.False(result.Authorized);
    }

    [Fact]
    public async Task ThrowsHttpRequestException_WhenLastDigitIsZero()
    {
        // Arrange
        var request = CreateRequest("2222405343248870");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.ProcessPaymentAsync(request));
    }
}
