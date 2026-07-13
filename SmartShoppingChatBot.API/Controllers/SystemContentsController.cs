using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.CreateSystemContent;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.DeleteSystemContent;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.GetAllSystemContent;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.GetSystemContentById;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.GetSystemContentByKey;
using SmartShoppingChatBot.Application.Features.SystemContentManagement.UpdateSystemContent;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/system-contents")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SystemContentsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SystemContentsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        [EndpointSummary("Admin creates a system content.")]
        [EndpointDescription("Admin creates a system content as draft or published.")]
        public async Task<IActionResult> CreateSystemContent([FromBody] CreateSystemContentCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "ADMIN")]
        [EndpointSummary("Admin updates a system content.")]
        [EndpointDescription("Admin updates a system content as draft or published.")]
        public async Task<IActionResult> UpdateSystemContent(
            [FromRoute] string id,
            [FromBody] UpdateSystemContentCommand command)
        {
            if (!ObjectId.TryParse(id, out var systemContentId))
            {
                return BadRequest(ApiResponse<SystemContentResponse>.Fail("Invalid system content ID."));
            }

            command.SystemContentId = systemContentId;
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "ADMIN")]
        [EndpointSummary("Admin deletes a system content.")]
        [EndpointDescription("Admin soft deletes a system content.")]
        public async Task<IActionResult> DeleteSystemContent([FromRoute] string id)
        {
            if (!ObjectId.TryParse(id, out var systemContentId))
            {
                return BadRequest(ApiResponse<SystemContentResponse>.Fail("Invalid system content ID."));
            }

            var result = await _mediator.Send(new DeleteSystemContentCommand
            {
                SystemContentId = systemContentId
            });

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet("key/{key}")]
        [EndpointSummary("Gets published system content by key.")]
        [EndpointDescription("Gets system content by key. Only published content is returned.")]
        public async Task<IActionResult> GetSystemContentByKey([FromRoute] string key)
        {
            var result = await _mediator.Send(new GetSystemContentByKeyQuery { Key = key });
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "ADMIN")]
        [EndpointSummary("Admin gets a system content by ID.")]
        [EndpointDescription("Admin gets a system content by ID.")]
        public async Task<IActionResult> GetSystemContentById([FromRoute] string id)
        {
            if (!ObjectId.TryParse(id, out var systemContentId))
            {
                return BadRequest(ApiResponse<SystemContentResponse>.Fail("Invalid system content ID."));
            }

            var result = await _mediator.Send(new GetSystemContentByIdQuery
            {
                SystemContentId = systemContentId
            });

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<SystemContentResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        [EndpointSummary("Admin gets all system contents.")]
        [EndpointDescription("Admin gets all system contents with optional filters.")]
        public async Task<IActionResult> GetAllSystemContents([FromQuery] GetAllSystemContentFilter filter)
        {
            var result = await _mediator.Send(new GetAllSystemContentQuery { Filter = filter });
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<object>>.Ok(result.Data!, result.Message));
        }
    }
}
