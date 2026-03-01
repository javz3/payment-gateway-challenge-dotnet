using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Clients;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public BankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/payments", request);

        response.EnsureSuccessStatusCode();

        var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>();

        return bankResponse ?? throw new InvalidOperationException("Bank returned an empty response.");
    }
}
