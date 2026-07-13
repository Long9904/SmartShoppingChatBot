using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1")]
    [ApiController]
    
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProductsController(IMediator mediator)
        {
            _mediator = mediator;
        }


        [HttpPost("products")]
        [EndpointDescription("Create a new product - internal api")]
        [EndpointSummary("Create a new product")]
        [ApiExplorerSettings(GroupName = "internal")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER, CATALOG_TEAM")]
        public async Task<IActionResult> CreateProductInternal([FromBody] ProductCreateCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<ProductResponse>.Ok(result.Data!, result.Message, result.MessageCode));

            return StatusCode(result.StatusCode, ApiResponse<ProductResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }


        [HttpPost("partner/products")]
        [EndpointDescription("Create a new product - external api")]
        [EndpointSummary("Create a new product")]
        [Authorize(AuthenticationSchemes = "ApiKey")]
        public async Task<IActionResult> CreateProductExternal([FromBody] ProductCreateCommand command)
        {
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<ProductResponse>.Ok(result.Data!, result.Message, result.MessageCode));

            return StatusCode(result.StatusCode, ApiResponse<ProductResponse>.Fail(result.Message!, result.Errors, result.MessageCode));
        }
    }
}
