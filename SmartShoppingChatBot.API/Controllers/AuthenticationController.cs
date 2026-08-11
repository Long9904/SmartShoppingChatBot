using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.Auth.GetMyProfile;
using SmartShoppingChatBot.Application.Features.Auth.Login;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ChangePassword;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ForgotPassword;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ResetPassword;
using SmartShoppingChatBot.Application.Features.ProfileManagement.UpdateProfile;

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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _mediator.Send(new GetMyProfileCommand());

        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message, result.MessageCode));

        return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    [HttpPut("update-profile")]
    [EndpointDescription("Update Profile")]
    [EndpointSummary("Update profile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message, result.MessageCode));
        return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    [HttpPut("change-password")]
    [EndpointDescription("Change Password")]
    [EndpointSummary("Change password")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<string>.Ok(result.Data!, result.Message, result.MessageCode));
        return StatusCode(result.StatusCode, ApiResponse<string>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    [HttpPost("forgot-password")]
    [EndpointDescription("Forgot Password")]
    [EndpointSummary("Forgot password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<string>.Ok(result.Data!, result.Message, result.MessageCode));

        return StatusCode(result.StatusCode, ApiResponse<string>.Fail(result.Message!, result.Errors, result.MessageCode));
    }

    
}
