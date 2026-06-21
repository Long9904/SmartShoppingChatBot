using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/payments")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }
        [HttpPost]
        [EndpointSummary("Create Payment Link ")]
        [EndpointDescription("Create a new payment link.")]
        public async Task<IActionResult> CreatePaymentLink(CreatePaymentRequest request)
        {
            var result = await _paymentService.CreatePaymentLink(request);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<PaymentResponsed>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<PaymentResponsed>.Fail(result.Message!, result.Errors));
        }
        [HttpGet("callback")]
        [EndpointSummary("[DON'T CALL THIS ENDPOINT DIRECTLY] WEBHOOK CALLBACK FOR PAYMENT")]
        [EndpointDescription("Handle payment success callback.")]
        public async Task<IActionResult> PaymentWebhook(PayOSWebhookRequest webhookData)
        {
            var result = await _paymentService.VerifyPaymentWebhook(webhookData);
            if (result)
                return StatusCode(StatusCodes.Status200OK, "Payment verified successfully.");
            return StatusCode(StatusCodes.Status400BadRequest, "Callback received but failed to process.");
        }

        [HttpPost("test-success")]
        [EndpointSummary("TEST ENDPOINT TO SIMULATE PAYMENT SUCCESS")]
        public async Task<IActionResult> TestPaymentSuccess(long orderCode)
        {
            var payment = await _paymentService.TestPaymentSuccessfull(orderCode);
            if (payment != null)
                return StatusCode(StatusCodes.Status200OK, "Payment simulated successfully.");
            return StatusCode(StatusCodes.Status400BadRequest, "Failed to simulate payment.");
        }
    }
}
