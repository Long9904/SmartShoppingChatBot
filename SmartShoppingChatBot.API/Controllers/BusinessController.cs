using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.Auth.GetMyBusinessProfile;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BOUpdateBusiness;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.GetBusinessConfig;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.ResetBusinessConfig;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.UpdateBusinessConfig;
using SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessRegistration;
using SmartShoppingChatBot.Application.Features.BusinessManagement.ConfirmBusinessRegistration;
using SmartShoppingChatBot.Application.Features.BusinessManagement.GetAllBusiness;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/businesses")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    public class BusinessController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BusinessController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [EndpointDescription("Registration a new business")]
        [EndpointSummary("BO registers a new business")]
        public async Task<IActionResult> CreateBusiness([FromBody] BusinessRegistrationCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessRegistrationResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<BusinessRegistrationResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        [EndpointDescription("Admin Get all businesses")]
        [EndpointSummary("Admin Get all businesses")]
        public async Task<IActionResult> GetAllBusinesses([FromQuery] GetBusinessesFilter query)
        {
            var result = await _mediator.Send(new GetBusinessesQuery { Filter = query });

            return Ok(ApiResponse<BasePaginatedList<BusinessResponse>>.Ok(result.Data!, result.Message));
        }

        [HttpGet("profile")]
        [Authorize(Roles = "BUSINESS_OWNER, CATALOG_TEAM, ADMIN")]
        [EndpointDescription("Business owner, catalog team views current business profile")]
        [EndpointSummary("BO, CT views current business profile")]
        public async Task<IActionResult> ViewBusinessProfile()
        {
            var result = await _mediator.Send(new GetMyBusinessProfileQuery());

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<MyBusinessProfileResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<MyBusinessProfileResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpPut("profile")]
        [Authorize(Roles = "BUSINESS_OWNER, CATALOG_TEAM")]
        [EndpointDescription("Business owner, catalog team updates current business profile")]
        [EndpointSummary("BO, CT updates current business profile")]
        public async Task<IActionResult> UpdateBusinessProfile([FromBody] UpdateBusinessCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<BusinessResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet("config")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        [EndpointDescription("Business owner gets the current business config")]
        [EndpointSummary("BO gets business config")]
        public async Task<IActionResult> GetBusinessConfig()
        {
            var result = await _mediator.Send(new GetBusinessConfigQuery());

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessConfigResponse>.Ok(result.Data!, result.Message, result.MessageCode));

            return StatusCode(result.StatusCode, ApiResponse<BusinessConfigResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }

        [HttpPut("config")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        [EndpointDescription("Business owner updates the current business config")]
        [EndpointSummary("BO updates business config")]
        public async Task<IActionResult> UpdateBusinessConfig([FromBody] UpdateBusinessConfigCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessConfigResponse>.Ok(result.Data!, result.Message, result.MessageCode));

            return StatusCode(result.StatusCode, ApiResponse<BusinessConfigResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }

        [HttpPut("config/default")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        [EndpointDescription("Business owner resets the current business config to default")]
        [EndpointSummary("BO resets business config to default")]
        public async Task<IActionResult> ResetBusinessConfig()
        {
            var result = await _mediator.Send(new ResetBusinessConfigCommand());

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessConfigResponse>.Ok(result.Data!, result.Message, result.MessageCode));

            return StatusCode(result.StatusCode, ApiResponse<BusinessConfigResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }

        [HttpPut("{id}/verify")]
        [Authorize(Roles = "ADMIN")]
        [EndpointSummary("Admin verify a business")]
        [EndpointDescription("Admin verify a business")]
        public async Task<IActionResult> VerifyBusiness([FromRoute] string id, [FromQuery] bool? isApproved)
        {
            if (!ObjectId.TryParse(id, out var businessId))
            {
                return BadRequest(ApiResponse<BusinessRegistrationResponse>.Fail("Invalid business ID."));
            }

            var command = new ConfirmBusinessCommand
            {
                BusinessId = businessId,
                IsApproved = isApproved
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, ApiResponse<BusinessRegistrationResponse>.Ok(result.Data!, result.Message));
            }

            return StatusCode(result.StatusCode, ApiResponse<BusinessRegistrationResponse>.Fail(result.Message!, result.Errors));
        }
    }
}
