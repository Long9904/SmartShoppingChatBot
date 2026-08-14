using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.DashboardManagement.AIUsageDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.RevenueDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.SubscriptionsDashboard;
using SmartShoppingChatBot.Application.Features.DashboardManagement.SummaryDashboard;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/dashboards")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DashboardController(IMediator mediator)
        {
            _mediator = mediator;
        }
        [HttpGet("subscriptions")]
        [EndpointDescription("Get subscription dashboard data")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Get([FromQuery] SubscriptionDashboardQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<SubscriptionDashboardResponse>>.Ok(result.Data!, result.Message, result.MessageCode));
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<SubscriptionDashboardResponse>>.Fail(result.Message!, result.Errors, result.MessageCode));
        }
        [HttpGet("revenue")]
        [EndpointDescription("Get revenue dashboard data")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetRevenue([FromQuery] RevenueDashboardQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<RevenueDashboardResponse>>.Ok(result.Data!, result.Message, result.MessageCode));
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<RevenueDashboardResponse>>.Fail(result.Message!, result.Errors, result.MessageCode));
        }
        [HttpGet("summary")]
        [EndpointDescription("Get summary dashboard data")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetSummary([FromQuery] SummaryDashboardQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<SummaryResponse>.Ok(result.Data!, result.Message, result.MessageCode));
            return StatusCode(result.StatusCode, ApiResponse<SummaryResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }
        [HttpGet("ai-usage")]
        [EndpointDescription("Get AI usage dashboard data")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetAIUsage([FromQuery] AIUsageDashboardQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<AIUsageDashboardResponse>.Ok(result.Data!, result.Message, result.MessageCode));
            return StatusCode(result.StatusCode, ApiResponse<AIUsageDashboardResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }
    }
}
