using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.BusinessRegistration;

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
    }
}
