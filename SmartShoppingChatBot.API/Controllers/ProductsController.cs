using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/product")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "external")]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProductsController(IMediator mediator)
        {
            _mediator = mediator;
        }


        [HttpPost]
        [EndpointDescription("Create a new product")]
        [EndpointSummary("Create a new product")]
        [Authorize(Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductCreateCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return StatusCode(result.StatusCode, ApiResponse<ProductResponse>.Ok(result.Data!, result.Message));
            return StatusCode(result.StatusCode, ApiResponse<ProductResponse>.Fail(result.Message!, result.Errors));
        }
    }
}
