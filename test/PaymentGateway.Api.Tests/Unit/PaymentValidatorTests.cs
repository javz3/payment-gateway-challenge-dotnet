using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validators;

namespace PaymentGateway.Api.Tests.Unit;

public class PaymentValidatorTests
{
    private readonly PaymentValidator _validator = new();

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 12,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };    

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RejectsPayment_WhenCardNumberIsNullOrEmpty(string cardNumber)
    {
        // Arrange
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("1234567890123")]       // 13 chars - too short
    [InlineData("12345678901234567890")] // 20 chars - too long
    public void RejectsPayment_WhenCardNumberLengthIsInvalid(string cardNumber)
    {
        // Arrange
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RejectsPayment_WhenCardNumberContainsNonNumericChars()
    {
        // Arrange
        var request = CreateValidRequest();
        request.CardNumber = "2222ABCD43248877";

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("12345678901234")]      // 14 chars - min valid
    [InlineData("1234567890123456789")]  // 19 chars - max valid
    public void AcceptsPayment_WhenCardNumberLengthIsValid(string cardNumber)
    {
        // Arrange
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.True(result);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void RejectsPayment_WhenExpiryMonthIsOutOfRange(int month)
    {
        // Arrange
        var request = CreateValidRequest();
        request.ExpiryMonth = month;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RejectsPayment_WhenExpiryDateIsInThePast()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;
        request.ExpiryMonth = 1;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void AcceptsPayment_WhenExpiryIsCurrentMonth()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = CreateValidRequest();
        request.ExpiryYear = now.Year;
        request.ExpiryMonth = now.Month;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XY")]
    [InlineData("GBPP")]
    [InlineData("JPY")]
    public void RejectsPayment_WhenCurrencyIsInvalid(string currency)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Currency = currency;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("gbp")]
    public void AcceptsPayment_WhenCurrencyIsValid(string currency)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Currency = currency;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsPayment_WhenAmountIsNotPositive(int amount)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Amount = amount;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]      // too short
    [InlineData("12345")]   // too long
    public void RejectsPayment_WhenCvvLengthIsInvalid(string cvv)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Cvv = cvv;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RejectsPayment_WhenCvvContainsNonNumericChars()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Cvv = "12A";

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    public void AcceptsPayment_WhenCvvLengthIsValid(string cvv)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Cvv = cvv;

        // Act
        bool result = _validator.IsValid(request);

        // Assert
        Assert.True(result);
    }
}
