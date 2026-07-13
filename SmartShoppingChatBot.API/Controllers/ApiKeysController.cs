using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.CreateNewKey;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.GetAllApiKey;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevealKey;
using SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevokeApiKey;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/api-keys")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    public class ApiKeysController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ApiKeysController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [EndpointDescription("Generate a new API key")]
        [EndpointSummary("BO Generate a new API key")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> GenerateApiKey([FromBody] CreateNewKeyCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<CreateApiKeyResponse>.Ok(result.Data!, result.Message, result.MessageCode));

            return StatusCode(result.StatusCode, ApiResponse<CreateApiKeyResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }


        [HttpGet]
        [EndpointDescription("Get all API keys")]
        [EndpointSummary("BO Get all API keys")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> GetAllApiKeys()
        {
            var result = await _mediator.Send(new GetAllApiKeyQuery());
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<ApiKeyResponse>>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<ApiKeyResponse>>.Fail(result.Message!, result.Errors));
        }


        [HttpGet("{apiKeyId}")]
        [EndpointDescription("Reveal a specific API key")]
        [EndpointSummary("BO Get a specific API key value")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> GetApiKey([FromRoute] string apiKeyId)
        {
            var result = await _mediator.Send(new RevealKeyQuery { KeyId = apiKeyId });
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<string>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<string>.Fail(result.Message!, result.Errors));
        }


        [HttpDelete("{id}")]
        [EndpointDescription("Revoke a specific API key")]
        [EndpointSummary("BO Revoke a specific API key")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> RevokeApiKey([FromRoute] string id)
        {
            var result = await _mediator.Send(new RevokeApiKeyCommand { Id = id });
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<string>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<string>.Fail(result.Message!, result.Errors));
        }
    }
}
