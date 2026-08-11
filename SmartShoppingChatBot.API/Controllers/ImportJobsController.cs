using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Features.ProductManagement.GetImportJobs;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v1/import-jobs")]
[ApiController]
[ApiExplorerSettings(GroupName = "internal")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = "BUSINESS_OWNER, CATALOG_TEAM")]
public class ImportJobsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ImportJobsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [EndpointSummary("BO,CT gets import job logs")]
    [EndpointDescription("Gets import jobs for the current business, with file name search and status filtering")]
    public async Task<IActionResult> GetImportJobs(
        [FromQuery] GetImportJobsFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetImportJobsQuery { Filter = filter },
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
