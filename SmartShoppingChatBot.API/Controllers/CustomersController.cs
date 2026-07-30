using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Features.ConversationManagement.CustomerGetConversations;
using SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;
using SmartShoppingChatBot.Application.Features.CustomerManagement.GetCustomers;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v1/customers")]
[ApiController]
[ApiExplorerSettings(GroupName = "internal")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [EndpointSummary("BO,CT gets customers of the current business")]
    [EndpointDescription(
        "Gets paged customers scoped to the current business, with CustomerExternalId and status filters.")]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] GetCustomersFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetCustomersQuery { Filter = filter },
            cancellationToken);

        return FromResult(result);
    }

    [HttpGet("{customerExternalId}/conversations")]
    [EndpointSummary("BO,CT gets conversations of a customer")]
    [EndpointDescription(
        "Gets paged conversations belonging to a CustomerExternalId in the current business.")]
    public async Task<IActionResult> GetCustomerConversations(
        [FromRoute] string customerExternalId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new CustomerGetConversationsQuery
            {
                ExternalCustomerId = customerExternalId,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            cancellationToken);

        return FromResult(result);
    }

    [HttpGet("{customerExternalId}/conversations/{conversationId}/messages")]
    [EndpointSummary("BO,CT gets a customer conversation messages")]
    [EndpointDescription(
        "Gets cursor-paged messages with content search and Customer/ChatBot sender filtering.")]
    public async Task<IActionResult> GetCustomerConversationDetail(
        [FromRoute] string customerExternalId,
        [FromRoute] string conversationId,
        [FromQuery] GetCustomerConversationDetailFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetCustomerConversationDetailQuery
            {
                CustomerExternalId = customerExternalId,
                ConversationId = conversationId,
                Filter = filter
            },
            cancellationToken);

        return FromResult(result);
    }

    private IActionResult FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return StatusCode(
                result.StatusCode,
                ApiResponse<T>.Ok(result.Data!, result.Message, result.MessageCode));
        }

        return StatusCode(
            result.StatusCode,
            ApiResponse<T>.Fail(result.Message!, result.Errors, result.MessageCode));
    }
}
