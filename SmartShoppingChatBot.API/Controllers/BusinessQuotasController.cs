using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Features.BusinessQuotaManagement.GetBusinessQuotas;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v1/business-quotas")]
[ApiController]
[ApiExplorerSettings(GroupName = "internal")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
public class BusinessQuotasController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessQuotasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [EndpointSummary("BO,CT gets usage logs for the current business quota")]
    [EndpointDescription("Gets a paginated list of usage logs belonging to the current business quota")]
    public async Task<IActionResult> GetUsageQuotaLogs(
        [FromQuery] GetBusinessQuotasFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetBusinessQuotasQuery { Filter = filter },
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
