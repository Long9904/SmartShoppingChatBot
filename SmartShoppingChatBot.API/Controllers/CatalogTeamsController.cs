using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.EmployeeRegistration;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/catalog-teams")]
    [ApiController]
    public class CatalogTeamsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CatalogTeamsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [EndpointDescription("Create a new catalog member")]
        [EndpointSummary("BO register catalog member")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> CreateCatalogMember([FromBody] EmployeeRegistrationCommand request)
        {
            var result = await _mediator.Send(request);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<EmployeeRegistrationResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<EmployeeRegistrationResponse>.Fail(result.Message!, result.Errors));
        }
    }
}
