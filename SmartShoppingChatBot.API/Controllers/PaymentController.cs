using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPayment;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPaymentByUser;
using SmartShoppingChatBot.Application.Features.PaymentManagement.GetPaymentByOrderCode;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/payments")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IMediator _mediator;

        public PaymentController(IPaymentService paymentService, IMediator mediator)
        {
            _paymentService = paymentService;
            _mediator = mediator;
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EndpointSummary("Create Payment Link ")]
        [EndpointDescription("Create a new payment link.")]
        public async Task<IActionResult> CreatePaymentLink(CreatePaymentRequest request)
        {
            var result = await _paymentService.CreatePaymentLink(request);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<PaymentResponsed>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<PaymentResponsed>.Fail(result.Message!, result.Errors));
        }

        [HttpPost("callback")]
        [AllowAnonymous]
        [EndpointSummary("[DON'T CALL THIS ENDPOINT DIRECTLY] WEBHOOK CALLBACK FOR PAYMENT")]
        [EndpointDescription("Handle payment success callback.")]
        public async Task<IActionResult> PaymentWebhook([FromBody] PayOSWebhookRequest webhookData)
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
            var payment = await _paymentService.TestPaymentSuccessful(orderCode);
            if (payment.IsSuccess)
                return StatusCode(StatusCodes.Status200OK, "Payment simulated successfully.");
            return StatusCode(StatusCodes.Status400BadRequest, "Failed to simulate payment.");
        }

        [HttpGet]
        [EndpointSummary("Admin-Get All Payments")]
        [Authorize(Roles = "Admin")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetAllPayments([FromQuery] GetPaymentQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<PaymentResponse>>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<PaymentResponse>>.Fail(result.Message!, result.Errors));
        }
        [HttpGet("user")]
        [EndpointSummary("BO-Get All Payments")]
        [Authorize(Roles = "BUSINESS_OWNER, CATALOG_TEAM")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetAllPaymentsByUser([FromQuery] GetPaymentByUserQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<PaymentResponse>>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<PaymentResponse>>.Fail(result.Message!, result.Errors));
        }

        [HttpGet("order/{orderCode}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetPaymentByOrderCode([FromRoute] long orderCode)
        {
            var query = new GetPaymentByOrderCodeQuery { OrderCode = orderCode };
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(StatusCodes.Status200OK, ApiResponse<PaymentResponse>.Ok(result.Data!, result.Message));
            return StatusCode(StatusCodes.Status404NotFound, ApiResponse<PaymentResponse>.Fail("Payment not found.", null));
        }
    }
}
