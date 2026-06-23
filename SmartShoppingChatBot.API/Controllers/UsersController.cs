using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.UserManagement.DeleteUser;
using SmartShoppingChatBot.Application.Features.UserManagement.GetAllUser;
using SmartShoppingChatBot.Application.Features.UserManagement.GetUserById;
using SmartShoppingChatBot.Application.Features.UserManagement.UpdateUser;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [EndpointDescription("Get all users")]
        [EndpointSummary("Get all users")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetAllUsers([FromQuery] GetAllUserQuery query)
        {
            var result = await _mediator.Send(query);
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<object>>.Ok(result.Data!, result.Message));
        }

        [HttpGet("{id}")]
        [EndpointDescription("Get user by ID")]
        [EndpointSummary("Get user by ID")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetUserById([FromRoute] string id)
        {
            var result = await _mediator.Send(new GetUserByIdQuery { UserId = id });
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<object>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.Message!, result.Errors));
        }

        [HttpDelete("{id}")]
        [EndpointDescription("Delete user by ID")]
        [EndpointSummary("Delete user by ID")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> DeleteUserById([FromRoute] string id)
        {
            var result = await _mediator.Send(new DeleteUserCommand { UserId = id });
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<object>.Ok(null, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.Message!, result.Errors));
        }

        [HttpPut("{id}")]
        [EndpointDescription("Update user by ID")]
        [EndpointSummary("Update user by ID")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> UpdateUserById([FromRoute] string id, [FromBody] UpdateUserCommand command)
        {
            if (!ObjectId.TryParse(id, out var userId))
            {
                return BadRequest(ApiResponse<ProfileResponse>.Fail("Invalid user ID."));
            }

            command.UserId = userId;

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<object>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.Message!, result.Errors));
        }
    }
}
