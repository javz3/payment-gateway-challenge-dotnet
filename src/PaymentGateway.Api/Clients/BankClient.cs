using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Clients;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankClient> _logger;

    public BankClient(HttpClient httpClient, ILogger<BankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync("/payments", request);
        }
        catch (HttpRequestException ex)
        {
            // Network-level failure (DNS, connection refused, timeout)
            _logger.LogError(ex, "Bank request failed: network error");
            throw new BankUnavailableException("Unable to reach the acquiring bank.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            int statusCode = (int)response.StatusCode;

            if (statusCode >= 400 && statusCode < 500)
            {
                _logger.LogError("Bank rejected request: {StatusCode}", statusCode);
                throw new BankRequestException($"Bank rejected the request with status {statusCode}.");
            }

            _logger.LogError("Bank returned server error: {StatusCode}", statusCode);
            throw new BankUnavailableException($"Bank returned status {statusCode}.");
        }

        BankPaymentResponse? bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>();

        return bankResponse ?? throw new InvalidOperationException("Bank returned an empty response.");
    }
}
