using System.Text.Json.Serialization;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validators;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Singleton: in-memory store must persist across requests. In production this would be
// Scoped with a real database — Singleton would prevent connection pooling and cause
// scalability issues under load.
builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentValidator, PaymentValidator>();

// AddHttpClient uses IHttpClientFactory under the hood — manages HttpMessageHandler
// lifetimes to avoid socket exhaustion and stale DNS. In production, this is where
// we'd add Polly retry policies for transient bank errors (503, timeouts).
builder.Services.AddHttpClient<IBankClient, BankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BankApi:BaseUrl"]!);
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
