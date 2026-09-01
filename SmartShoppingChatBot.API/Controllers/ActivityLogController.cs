using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ActivityLogManagement.GetActivityLog;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/activity-logs")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ActivityLogController : ControllerBase
    {
        private readonly IMediator mediator;

        public ActivityLogController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] GetActivityLogQuery query)
        {
            var result = await mediator.Send(query);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, ApiResponse<ActivityLogResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
            }

            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<ActivityLogResponse>>.Ok(result.Data!, result.Message, result.MessageCode));
        }
    }
}
