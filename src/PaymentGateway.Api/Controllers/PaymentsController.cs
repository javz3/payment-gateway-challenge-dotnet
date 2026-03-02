using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PaymentResponse?> GetPayment(Guid id)
    {
        try
        {
            PaymentResponse? payment = _paymentService.GetPayment(id);

            if (payment is null)
            {
                return NotFound();
            }

            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while retrieving payment {PaymentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An internal error occurred while retrieving the payment." });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> PostPaymentAsync([FromBody] PostPaymentRequest request)
    {
        try
        {
            PaymentResponse result = await _paymentService.ProcessPaymentAsync(request);

            if (result.Status == PaymentStatus.Rejected)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (BankUnavailableException ex)
        {
            _logger.LogError(ex, "Bank unavailable while processing payment");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The payment provider is currently unavailable. Please try again later." });
        }
        catch (BankRequestException ex)
        {
            // A 4xx from the bank means our request mapping has a bug — not the merchant's fault.
            _logger.LogError(ex, "Bank rejected our request — possible mapping bug");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An internal error occurred while processing the payment." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing payment");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An internal error occurred while processing the payment." });
        }
    }
}
