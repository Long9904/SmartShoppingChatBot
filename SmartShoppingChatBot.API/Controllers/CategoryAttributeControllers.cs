using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.ActivateVersion;
using SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.CreateDraft;
using SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetAllSchemas;
using SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetHistory;
using SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetPendingApproval;
using SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.UpdateAttributeDefinition;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v1/category-attributes")]
[ApiController]
[ApiExplorerSettings(GroupName = "internal")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
public sealed class CategoryAttributeControllers : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoryAttributeControllers(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("drafts")]
    [EndpointSummary("Creates a new draft version of a category attribute schema.")]
    public async Task<IActionResult> CreateDraftAsync([FromBody] CreateDraftCommand command)
    {
        var result = await _mediator.Send(command);
        return ToResponse(result);
    }

    [HttpGet]
    [EndpointSummary("Gets the highest schema version of every category.")]
    public async Task<IActionResult> GetAllSchemasAsync()
    {
        var result = await _mediator.Send(new GetAllSchemasQuery());
        return ToResponse(result);
    }

    [HttpPut("activate")]
    [EndpointSummary("Activates a category attribute schema version.")]
    public async Task<IActionResult> ActivateVersionAsync([FromQuery] string category, [FromQuery] int version)
    {
        var result = await _mediator.Send(new ActivateVersionCommand
        {
            Category = category,
            Version = version
        });

        if (result.IsSuccess)
        {
            return StatusCode(result.StatusCode, ApiResponse<bool>.Ok(result.Data, result.Message, result.MessageCode));
        }

        return StatusCode(result.StatusCode, ApiResponse<bool>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    [HttpGet("pending-approval")]
    [EndpointSummary("Gets category attribute schema versions pending approval.")]
    public async Task<IActionResult> GetPendingApprovalAsync()
    {
        var result = await _mediator.Send(new GetPendingApprovalQuery());
        return ToResponse(result);
    }

    [HttpGet("history")]
    [EndpointSummary("Gets all schema versions for a category.")]
    public async Task<IActionResult> GetHistoryAsync([FromQuery] string category)
    {
        var result = await _mediator.Send(new GetHistoryQuery { Category = category });
        return ToResponse(result);
    }

    [HttpPut("attributes/{attributeKey}")]
    [EndpointSummary("Updates an attribute definition without changing its data type.")]
    public async Task<IActionResult> UpdateAttributeDefinitionAsync(
        [FromRoute] string attributeKey,
        [FromQuery] string category,
        [FromBody] UpdateAttributeDefinitionCommand command)
    {
        command.Category = category;
        command.AttributeKey = attributeKey;
        var result = await _mediator.Send(command);

        return ToResponse(result);
    }

    private IActionResult ToResponse(Result<CategoryAttributeSchemaResponse> result)
    {
        if (result.IsSuccess)
        {
            return StatusCode(
                result.StatusCode,
                ApiResponse<CategoryAttributeSchemaResponse>.Ok(result.Data!, result.Message, result.MessageCode));
        }

        return StatusCode(
            result.StatusCode,
            ApiResponse<CategoryAttributeSchemaResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    private IActionResult ToResponse(Result<List<CategoryAttributeSchemaResponse>> result)
    {
        if (result.IsSuccess)
        {
            return StatusCode(
                result.StatusCode,
                ApiResponse<List<CategoryAttributeSchemaResponse>>.Ok(result.Data!, result.Message, result.MessageCode));
        }

        return StatusCode(
            result.StatusCode,
            ApiResponse<List<CategoryAttributeSchemaResponse>>.Fail(result.Message!, result.Errors, result.MessageCode));
    }
}
