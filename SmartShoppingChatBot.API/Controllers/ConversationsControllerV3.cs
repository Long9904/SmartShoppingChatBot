using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v3/chat/conversations")]
[ApiExplorerSettings(GroupName = "external")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[ApiController]
public sealed class ConversationsControllerV3(IMediator mediator) : ControllerBase
{
    //[HttpPost("messages")]
    //[EndpointDescription("Customer send a message with V3 product search")]
    //[EndpointSummary("Customer send a message V3")]
    //public Task<IActionResult> SendChatMessageV3(
    //    [FromBody] SendMessageCommandV3 command,
    //    CancellationToken cancellationToken)
    //{
    //    return SendCore(command, cancellationToken);
    //}

    //[HttpPost("{conversationId}/messages")]
    //[EndpointDescription("Customer continue a conversation with V3 product search")]
    //[EndpointSummary("Customer continue a conversation V3")]
    //public Task<IActionResult> ContinueChatMessageV3(
    //    [FromRoute] string conversationId,
    //    [FromBody] SendMessageCommandV3 command,
    //    CancellationToken cancellationToken)
    //{
    //    command.ConversationId = conversationId;
    //    return SendCore(command, cancellationToken);
    //}

    //// Keep the existing API result envelope for clients using the V3 route.
    //private async Task<IActionResult> SendCore(
    //    SendMessageCommandV3 command,
    //    CancellationToken cancellationToken)
    //{
    //    var result = await mediator.Send(command, cancellationToken);

    //    if (result.IsSuccess)
    //    {
    //        return StatusCode(
    //            result.StatusCode,
    //            ApiResponse<ConversationResponse>.Ok(
    //                result.Data!,
    //                result.Message,
    //                result.MessageCode));
    //    }

    //    return StatusCode(
    //        result.StatusCode,
    //        ApiResponse<ConversationResponse>.Fail(
    //            result.Message!,
    //            result.Errors,
    //            result.MessageCode));
    //}
}
