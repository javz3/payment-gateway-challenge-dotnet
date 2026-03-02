# Payment Gateway

A payment gateway API that validates merchant payment requests, forwards them to an acquiring bank, and allows retrieval of payment details.

## Running

Start the bank simulator then the API:

```bash
docker-compose up -d
dotnet run --project src/PaymentGateway.Api
```

Swagger UI: https://localhost:7092/swagger

## Testing

```bash
dotnet test
```

51 tests: unit tests for validation and service logic, integration tests for the full HTTP flow and bank simulator. The 3 bank integration tests require the bank simulator to be running (`docker-compose up -d`). All other tests run without Docker.

## Architecture

```
┌──────────┐     ┌────────────────┐     ┌────────────┐     ┌──────────────┐
│  Client  │────>│  Payments      │────>│  Payment   │────>│  Bank Client │───> Bank
│ (Merchant)│<────│  Controller    │<────│  Service   │<────│  (HTTP)      │<─── Simulator
└──────────┘     └────────────────┘     └─────┬──────┘     └──────────────┘
                       │                      │
                       │                ┌─────┴──────┐
                       │                │  Payment   │
                       │                │  Validator │
                       │                └────────────┘
                       │                      │
                       │                ┌─────┴──────┐
                       └───────────────>│  Payments  │
                                        │  Repository│
                                        └────────────┘
```

Each layer communicates through interfaces, making every component independently testable and swappable.

## API Examples

### POST /api/Payments

**Authorized** (card ending in odd digit):
```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

**Declined** (card ending in even digit):
```json
{
  "cardNumber": "2222405343248878",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

**Rejected** (invalid data → 400):
```json
{
  "cardNumber": "123",
  "expiryMonth": 13,
  "expiryYear": 2020,
  "currency": "XYZ",
  "amount": -1,
  "cvv": "AB"
}
```

**Bank unavailable** (card ending in 0 → 502): the bank simulator returns 503, which the gateway surfaces as 502 Bad Gateway with an error message.

### GET /api/Payments/{id}

Use the `id` from any POST response. The card number is masked to the last four digits:
```json
{
  "id": "3fa85f64-...",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100
}
```

## Approach

I kept the architecture simple: Controller, Service, Validator, BankClient, Repository — with interfaces between layers so each piece is independently testable. The controller is a thin HTTP adapter that only depends on `IPaymentService` — both GET and POST go through the service, keeping all business logic in one place.

**Validation** is extracted into its own `PaymentValidator` class behind an `IPaymentValidator` interface, following the Single Responsibility Principle. The expiry check requires cross-field logic (month + year combined), and a dedicated class is easier to test and debug than attribute-based validation for this size of problem.

**Card number masking** happens at the service layer when constructing the response. The full card number is never stored — only the last four digits make it into the repository. This aligns with PCI compliance principles.

**Bank error handling** uses custom exceptions so HTTP details from the bank don't leak into the service or controller layers. The bank client checks `response.IsSuccessStatusCode` rather than using `EnsureSuccessStatusCode()`, keeping HTTP concerns encapsulated. A 4xx from the bank throws `BankRequestException` (indicates a bug in our request mapping → 500), while 5xx or network failures throw `BankUnavailableException` (transient issue → 502 Bad Gateway). Both surface meaningful error messages to the merchant.

**Logging** uses the built-in `ILogger<T>` at each layer — payment outcomes, validation failures, and bank errors are all logged with structured parameters. Full card numbers are never logged.

## Project structure

```
src/PaymentGateway.Api/
    Controllers/    - API endpoints (thin HTTP adapter)
    Services/       - business logic and orchestration
    Validators/     - request validation (SRP)
    Clients/        - HTTP client for the acquiring bank
    Repositories/   - in-memory payment storage
    Exceptions/     - custom exceptions (BankUnavailableException, BankRequestException)
    Models/         - request/response DTOs, bank models, enums
test/PaymentGateway.Api.Tests/
    Unit/           - service and validator tests with mocked dependencies
    Integration/    - full HTTP pipeline and bank simulator tests
imposters/          - bank simulator configuration (unchanged)
```

## Assumptions

- Three supported currencies: GBP, USD, EUR
- Amount must be a positive integer (zero or negative is rejected)
- Expiry dates are validated against UTC - a card is valid if its month/year is the current month or later
- Bank base URL (`http://localhost:8080`) is configured in `appsettings.json`

## Changes from starter code

- **`CardNumber` and `Cvv` changed from `int` to `string`** - preserves leading zeros and enables length/character validation
- **Merged `PostPaymentResponse` and `GetPaymentResponse`** into a single `PaymentResponse` - the fields were identical
- **Extracted `IPaymentsRepository` interface** - enables mocking in tests and swapping the storage implementation
- **Fixed GET to return 404** when a payment isn't found (original always returned 200)
- **Repository uses `ConcurrentDictionary`** - thread-safe for the Singleton lifetime, O(1) lookup
- **Moved `PaymentStatus` from `Enums/` to `Models/`** - the file's namespace was already `PaymentGateway.Api.Models`, so the folder didn't match; also co-locates it with the models that reference it
- **Separated `Services/` into `Services/`, `Clients/`, `Repositories/`** - each folder reflects its concern rather than grouping all dependencies under a single folder

## What I'd add for production

**Resiliency**
- Retry with exponential backoff for transient bank errors using Polly (503s, timeouts, network blips are typically safe to retry)
- Circuit breaker to fail fast when the bank is consistently unavailable
- Request timeouts on the HTTP client to prevent thread starvation

**Observability**
- Structured logging with correlation IDs for end-to-end payment tracing (at 118 118 Money I used Serilog with Seq for this across 18 microservices)
- Distributed tracing (OpenTelemetry) to trace requests across the gateway and bank
- Health check endpoint (`/health`) that verifies bank connectivity

**Security & Compliance**
- Authentication and rate limiting per merchant
- Idempotency keys to prevent duplicate charges
- Field-level validation error responses for rejected payments
- Encryption at rest for stored payment data

**Data**
- Persistent storage (SQL/Redis) with the repository registered as Scoped, not Singleton
- Domain model separation — repository stores a `Payment` entity, service maps to/from response DTOs
