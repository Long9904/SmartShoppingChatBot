using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.BusinessRegistration;
using SmartShoppingChatBot.Application.Features.ConfirmBusinessRegistration;
using SmartShoppingChatBot.Application.Features.GetAllBusiness;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/businesses")]
    [ApiController]
    public class BusinessController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BusinessController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [EndpointDescription("Registration a new business")]
        public async Task<IActionResult> CreateBusiness([FromBody] BusinessRegistrationCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessRegistrationResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<BusinessRegistrationResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        [EndpointDescription("Get all businesses")]
        public async Task<IActionResult> GetAllBusinesses([FromQuery] GetBusinessesQuery query)
        {
            var result = await _mediator.Send(query);

            return Ok(ApiResponse<BasePaginatedList<BusinessResponse>>.Ok(result.Data!, result.Message));
        }

        [HttpPut("{id}/verify")]
        [Authorize(Roles = "ADMIN")]
        [EndpointDescription("Verify a business")]
        public async Task<IActionResult> VerifyBusiness([FromRoute] string id, [FromQuery] bool isApproved)
        {
            var command = new ConfirmBusinessCommand
            {
                BusinessId = ObjectId.Parse(id),
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
