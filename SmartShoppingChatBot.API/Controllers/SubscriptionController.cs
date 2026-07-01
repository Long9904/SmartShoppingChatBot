using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.CreateSubscription;
using SmartShoppingChatBot.Application.Features.DeleteSubscription;
using SmartShoppingChatBot.Application.Features.GetAllSubscription;
using SmartShoppingChatBot.Application.Features.UpdateSubscription;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/subscriptions")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    public class SubscriptionController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SubscriptionController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [EndpointSummary("Admin add a new subscription.")]
        [EndpointDescription("Add a new subscription.")]
        public async Task<IActionResult> AddSubscription([FromBody] SubscriptionAddCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SubscriptionResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        [EndpointSummary(" Get all subscriptions with optional filters.")]
        [EndpointDescription("Get all subscriptions with optional filters.")]
        public async Task<IActionResult> GetSubscriptions([FromQuery] GetSubscriptionQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<SubscriptionResponse>>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        [EndpointSummary("Admin update an existing subscription.")]
        public async Task<IActionResult> UpdateSubscription([FromRoute] string id, [FromBody] SubscriptionUpdateCommand command)
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SubscriptionResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        [EndpointSummary("Admin Delete a subscription by ID.")]
        public async Task<IActionResult> DeleteSubscription([FromRoute] string id)
        {
            var command = new DeleteSubscriptionCommand { Id = id };
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<string>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, result);
        }
    }
}
