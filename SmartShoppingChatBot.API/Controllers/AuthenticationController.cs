using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.Auth.GetMyProfile;
using SmartShoppingChatBot.Application.Features.Auth.Login;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v1/auth")]
[ApiController]
[ApiExplorerSettings(GroupName = "internal")]
public class AuthenticationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthenticationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [EndpointDescription("Login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<LoginResponse>.Ok(result.Data!, result.Message, result.MessageCode));

        return StatusCode(result.StatusCode, ApiResponse<LoginResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    [HttpGet("me")]
    [EndpointDescription("Get My Profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _mediator.Send(new GetMyProfileCommand());

        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message, result.MessageCode));

        return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
    }
}
