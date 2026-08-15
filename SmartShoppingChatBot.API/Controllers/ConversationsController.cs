using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ConversationManagement.CustomerGetConversations;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory;
using SmartShoppingChatBot.Application.Features.ConversationManagement.RegisterConversationOrder;
using SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessage;
using SmartShoppingChatBot.Application.Features.ConversationManagement.UpdateConversationOrderStatus;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/chat/conversations")]
    [ApiExplorerSettings(GroupName = "external")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    [ApiController]
    public class ConversationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ConversationsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("messages")]
        [EndpointDescription("Customer send a message")]
        [EndpointSummary("Customer send a message")]
        public async Task<IActionResult> SendChatMessageV1(
            [FromBody] SendMessageCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return StatusCode(
                    result.StatusCode,
                    ApiResponse<ConversationResponse>.Ok(
                        result.Data!,
                        result.Message,
                        result.MessageCode));
            }

            return StatusCode(
                result.StatusCode,
                ApiResponse<ConversationResponse>.Fail(
                    result.Message!,
                    result.Errors,
                    result.MessageCode));
        }


        [HttpPost("{conversationId}/messages")]
        [EndpointDescription("Customer send a message")]
        [EndpointSummary("Customer send a message")]
        public async Task<IActionResult> SendChatMessageV2(
            [FromRoute] string? conversationId,
            [FromBody] SendMessageCommand command,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(conversationId))
                command.ConversationId = conversationId;
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return StatusCode(
                    result.StatusCode,
                    ApiResponse<ConversationResponse>.Ok(
                        result.Data!,
                        result.Message,
                        result.MessageCode));
            }

            return StatusCode(
                result.StatusCode,
                ApiResponse<ConversationResponse>.Fail(
                    result.Message!,
                    result.Errors,
                    result.MessageCode));
        }

        [HttpPost("{conversationId}/orders")]
        [EndpointDescription(
            "Registers the external order-to-conversation mapping before checkout")]
        [EndpointSummary("Register a conversation order")]
        public async Task<IActionResult> RegisterOrder(
            [FromRoute] string conversationId,
            [FromBody] RegisterConversationOrderRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new RegisterConversationOrderCommand
                {
                    ConversationId = conversationId,
                    Order = request
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, ApiResponse<ConversationOrderResponse>.Ok(
                        result.Data!, result.Message, result.MessageCode));
            }

            return StatusCode(result.StatusCode, ApiResponse<ConversationOrderResponse>.Fail(
                    result.Message!, result.Errors, result.MessageCode));
        }

        [HttpPatch("orders/{externalOrderId}/status")]
        [EndpointDescription(
            "Updates an order status by the external order ID previously registered for the current business")]
        [EndpointSummary("Update a conversation order status")]
        public async Task<IActionResult> UpdateOrderStatus(
            [FromRoute] string externalOrderId,
            [FromBody] UpdateConversationOrderStatusRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new UpdateConversationOrderStatusCommand
                {
                    ExternalOrderId = externalOrderId,
                    Status = request.Status
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return StatusCode(
                    result.StatusCode,
                    ApiResponse<ConversationOrderResponse>.Ok(
                        result.Data!, result.Message, result.MessageCode));
            }

            return StatusCode(
                result.StatusCode,
                ApiResponse<ConversationOrderResponse>.Fail(
                    result.Message!, result.Errors, result.MessageCode));
        }



        [HttpGet("{conversationId}/messages")]
        [EndpointDescription("Get customer chat history")]
        [EndpointSummary("Customer get conversation messages")]
        public async Task<IActionResult> GetChatHistory(
            [FromRoute] string conversationId,
            [FromQuery] string externalCustomerId,
            [FromQuery] string? lastCursor = null,
            [FromQuery] int limit = 20,
            CancellationToken cancellationToken = default)
        {
            var query = new GetChatHistoryQuery
            {
                ConversationId = conversationId,
                ExternalCustomerId = externalCustomerId,
                LastCursor = lastCursor,
                Limit = limit
            };

            var result = await _mediator.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return StatusCode(
                    result.StatusCode,
                    ApiResponse<CursorPage<ConversationMessageResponse>>.Ok(
                        result.Data!,
                        result.Message,
                        result.MessageCode));
            }

            return StatusCode(
                result.StatusCode,
                ApiResponse<CursorPage<ConversationMessageResponse>>.Fail(
                    result.Message!,
                    result.Errors,
                    result.MessageCode));
        }

        [HttpGet]
        [EndpointDescription("Get all conversations of a customer")]
        [EndpointSummary("Customer get all conversations")]
        public async Task<IActionResult> GetAllCustomerConverstation(
            [FromQuery] string externalCustomerId,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var query = new CustomerGetConversationsQuery
            {
                ExternalCustomerId = externalCustomerId,
                PageIndex = pageIndex,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return StatusCode(
                    result.StatusCode,
                    ApiResponse<BasePaginatedList<CustomerConversationResponse>>.Ok(
                        result.Data!,
                        result.Message,
                        result.MessageCode));
            }

            return StatusCode(
                result.StatusCode,
                ApiResponse<BasePaginatedList<CustomerConversationResponse>>.Fail(
                    result.Message!,
                    result.Errors,
                    result.MessageCode));
        }
    }
}
