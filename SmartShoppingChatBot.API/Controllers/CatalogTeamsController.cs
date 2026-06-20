using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.BusinessMemberRegistration;
using SmartShoppingChatBot.Application.Features.DeleteBusinessMember;
using SmartShoppingChatBot.Application.Features.GetAllBusinessMember;
using SmartShoppingChatBot.Application.Features.GetBusinessMemberById;
using SmartShoppingChatBot.Application.Features.UpdateBusinessMember;
using SmartShoppingChatBot.Domain.Commons;

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
        public async Task<IActionResult> CreateCatalogMember([FromBody] MemberRegistrationCommand request)
        {
            var result = await _mediator.Send(request);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BusinessMemberRegistrationResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<BusinessMemberRegistrationResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpGet]
        [EndpointDescription("Business owner gets catalog team members in current business")]
        [EndpointSummary("BO gets catalog team members")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> GetCatalogMembers([FromQuery] GetBusinessMemberFilter query)
        {
            var result = await _mediator.Send(new GetBusinessMemberQuery { Filter = query });

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<object>>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<object>>.Fail(result.Message!, result.Errors));
        }

        [HttpGet("{id}")]
        [EndpointDescription("Business owner gets a catalog team member in current business by ID")]
        [EndpointSummary("BO gets catalog team member by ID")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> GetCatalogMemberById([FromRoute] string id)
        {
            if (!ObjectId.TryParse(id, out var memberId))
            {
                return BadRequest(ApiResponse<ProfileResponse>.Fail("Invalid member ID."));
            }

            var result = await _mediator.Send(new GetBusinessMemberByIdQuery
            {
                MemberId = memberId
            });

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpPut("{id}")]
        [EndpointDescription("Business owner updates a catalog team member in current business")]
        [EndpointSummary("BO updates catalog team member")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> UpdateCatalogMember([FromRoute] string id, [FromBody] UpdateBusinessMemberCommand command)
        {
            if (!ObjectId.TryParse(id, out var memberId))
            {
                return BadRequest(ApiResponse<ProfileResponse>.Fail("Invalid member ID."));
            }

            command.MemberId = memberId;
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors));
        }

        [HttpDelete("{id}")]
        [EndpointDescription("Business owner deletes a catalog team member in current business")]
        [EndpointSummary("BO deletes catalog team member")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> DeleteCatalogMember([FromRoute] string id)
        {
            if (!ObjectId.TryParse(id, out var memberId))
            {
                return BadRequest(ApiResponse<ProfileResponse>.Fail("Invalid member ID."));
            }

            var result = await _mediator.Send(new DeleteBusinessMemberCommand
            {
                MemberId = memberId
            });

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Ok(result.Data!, result.Message));

            return StatusCode(result.StatusCode, ApiResponse<ProfileResponse>.Fail(result.Message!, result.Errors));
        }
    }
}
