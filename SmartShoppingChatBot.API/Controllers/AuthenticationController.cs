using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.Login;
using SmartShoppingChatBot.Application.Features.SelectBusiness;

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

    [HttpGet("select-business/{businessId}")]
    [Authorize]
    [EndpointDescription("Select business")]
    public async Task<IActionResult> SelectBusiness(string businessId)
    {
        var command = new SelectBusinessCommand
        {
            BusinessId = ObjectId.Parse(businessId)
        };
        var result = await _mediator.Send(command);
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<SelectBusinessResponse>.Ok(result.Data!, result.Message));
        return StatusCode(result.StatusCode, ApiResponse<SelectBusinessResponse>.Fail(result.Message!, result.Errors));
    }
}
