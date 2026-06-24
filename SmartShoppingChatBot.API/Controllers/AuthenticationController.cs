using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.GetMyProfile;
using SmartShoppingChatBot.Application.Features.Login;
using SmartShoppingChatBot.Application.Features.UpdateMyProfile;

namespace SmartShoppingChatBot.API.Controllers;

[Route("api/v1/auth")]
[ApiController]
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
            return StatusCode(result.StatusCode, ApiResponse<LoginResponse>.Ok(result.Data!, result.Message));
        return StatusCode(result.StatusCode, ApiResponse<LoginResponse>.Fail(result.Message!, result.Errors));
    }

    [HttpGet("me")]
    [EndpointDescription("Get My Profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _mediator.Send(new GetMyProfileCommand());
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message));
        return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors));
    }
    [HttpPut("update-profile")]
    [EndpointDescription("Update My Profile")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message));
        return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors));
    }
}
