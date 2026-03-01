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

47 tests: unit tests for validation and service logic, integration tests for the full HTTP flow and bank simulator. The 3 bank integration tests require the bank simulator to be running (`docker-compose up -d`). All other tests run without Docker.

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

**Bank unavailable** (card ending in 0 → 502): the bank simulator returns 503, which the gateway surfaces as 502 Bad Gateway.

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

I kept the architecture simple: Controller, Service, BankClient, Repository — with interfaces between layers so each piece is independently testable. The controller is a thin HTTP adapter that only depends on `IPaymentService` — both GET and POST go through the service, keeping all business logic in one place.

**Validation** lives in `PaymentService` rather than data annotations. The expiry check requires cross-field logic (month + year combined), and a dedicated method is easier to test and debug than attribute-based validation for this size of problem.

**Card number masking** happens at the service layer when constructing the response. The full card number is never stored — only the last four digits make it into the repository. This aligns with PCI compliance principles.

**Bank 503 handling** surfaces as HTTP 502 Bad Gateway to the merchant. I chose not to add retry logic (e.g. Polly) since the spec doesn't require it, but this would be the natural next step for production readiness.

## Project structure

```
src/PaymentGateway.Api/
    Controllers/    - API endpoints
    Services/       - business logic and validation
    Clients/        - HTTP client for the acquiring bank
    Repositories/   - in-memory payment storage
    Models/         - request/response DTOs, bank models, enums
test/PaymentGateway.Api.Tests/
    Unit/           - service tests with mocked dependencies
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

- Retry with exponential backoff for bank timeouts (Polly)
- Idempotency keys to prevent duplicate charges
- Structured logging with correlation IDs for payment audit trails
- Authentication and rate limiting per merchant
- Persistent storage (replacing the in-memory repository)
- Field-level validation error responses for rejected payments
