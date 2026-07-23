using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ImportProductExcel;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductDelete;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetAll;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetById;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductUpdate;

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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
        public async Task<IActionResult> CreateProductInternal([FromBody] ProductCreateCommand command)
        {
            var result = await _mediator.Send(command);
            return FromResult(result);
        }


        [HttpPost("partner/products")]
        [EndpointDescription("Create a new product - external api")]
        [EndpointSummary("Create a new product")]
        [ApiExplorerSettings(GroupName = "external")]
        [Authorize(AuthenticationSchemes = "ApiKey")]
        public async Task<IActionResult> CreateProductExternal([FromBody] ProductCreateCommand command)
        {
            var result = await _mediator.Send(command);
            return FromResult(result);
        }

        [HttpPut("products/{id}")]
        [EndpointDescription("Update a product by its Mongo ID - internal API")]
        [EndpointSummary("Update a product")]
        [ApiExplorerSettings(GroupName = "internal")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
        public async Task<IActionResult> UpdateProductInternal(
            [FromRoute] string id,
            [FromBody] ProductUpdateCommand command)
        {
            if (!ObjectId.TryParse(id, out var productId))
            {
                return BadRequest(ApiResponse<ProductResponse>.Fail(
                    "Invalid product ID.",
                    messageCode: ProductMessageCode.InvalidId));
            }

            command.ProductId = productId;
            var result = await _mediator.Send(command);
            return FromResult(result);
        }

        [HttpPut("partner/products/{externalId}")]
        [EndpointDescription("Update a product by its external ID - external API")]
        [EndpointSummary("Update a product")]
        [ApiExplorerSettings(GroupName = "external")]
        [Authorize(AuthenticationSchemes = "ApiKey")]
        public async Task<IActionResult> UpdateProductExternal(
            [FromRoute] string externalId,
            [FromBody] ProductUpdateCommand command)
        {
            command.LookupExternalId = externalId;
            var result = await _mediator.Send(command);
            return FromResult(result);
        }

        [HttpDelete("products/{id}")]
        [EndpointDescription("Soft delete a product by its Mongo ID - internal API")]
        [EndpointSummary("Delete a product")]
        [ApiExplorerSettings(GroupName = "internal")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
        public async Task<IActionResult> DeleteProductInternal([FromRoute] string id)
        {
            if (!ObjectId.TryParse(id, out var productId))
            {
                return BadRequest(ApiResponse<ProductResponse>.Fail(
                    "Invalid product ID.",
                    messageCode: ProductMessageCode.InvalidId));
            }

            var result = await _mediator.Send(new ProductDeleteCommand { ProductId = productId });
            return FromResult(result);
        }

        [HttpDelete("partner/products/{externalId}")]
        [EndpointDescription("Soft delete a product by its external ID - external API")]
        [EndpointSummary("Delete a product")]
        [ApiExplorerSettings(GroupName = "external")]
        [Authorize(AuthenticationSchemes = "ApiKey")]
        public async Task<IActionResult> DeleteProductExternal([FromRoute] string externalId)
        {
            var result = await _mediator.Send(new ProductDeleteCommand { ExternalId = externalId });
            return FromResult(result);
        }

        [HttpGet("products/{id}")]
        [EndpointDescription("Get a product by its Mongo ID - internal API")]
        [EndpointSummary("Get a product by ID")]
        [ApiExplorerSettings(GroupName = "internal")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
        public async Task<IActionResult> GetProductByIdInternal([FromRoute] string id)
        {
            if (!ObjectId.TryParse(id, out var productId))
            {
                return BadRequest(ApiResponse<ProductResponse>.Fail(
                    "Invalid product ID.",
                    messageCode: ProductMessageCode.InvalidId));
            }

            var result = await _mediator.Send(new ProductGetByIdQuery { ProductId = productId });
            return FromResult(result);
        }

        [HttpGet("partner/products/{externalId}")]
        [EndpointDescription("Get a product by its external ID - external API")]
        [EndpointSummary("Get a product by external ID")]
        [ApiExplorerSettings(GroupName = "external")]
        [Authorize(AuthenticationSchemes = "ApiKey")]
        public async Task<IActionResult> GetProductByIdExternal([FromRoute] string externalId)
        {
            var result = await _mediator.Send(new ProductGetByIdQuery { ExternalId = externalId });
            return FromResult(result);
        }

        [HttpGet("products")]
        [EndpointDescription("Get products with fixed product-field filters - internal API")]
        [EndpointSummary("Get all products")]
        [ApiExplorerSettings(GroupName = "internal")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER,CATALOG_TEAM")]
        public async Task<IActionResult> GetAllProductsInternal([FromQuery] ProductGetAllFilter filter)
        {
            var result = await _mediator.Send(new ProductGetAllQuery { Filter = filter });
            return FromResult(result);
        }

        [HttpGet("partner/products")]
        [EndpointDescription("Get products with fixed product-field filters - external API")]
        [EndpointSummary("Get all products")]
        [ApiExplorerSettings(GroupName = "external")]
        [Authorize(AuthenticationSchemes = "ApiKey")]
        public async Task<IActionResult> GetAllProductsExternal([FromQuery] ProductGetAllFilter filter)
        {
            var result = await _mediator.Send(new ProductGetAllQuery { Filter = filter });
            return FromResult(result);
        }

        private IActionResult FromResult<T>(Result<T> result)
        {
            if (result.IsSuccess)
            {
                return StatusCode(
                    result.StatusCode,
                    ApiResponse<T>.Ok(result.Data!, result.Message, result.MessageCode));
            }

            return StatusCode(
                result.StatusCode,
                ApiResponse<T>.Fail(result.Message!, result.Errors, result.MessageCode));
        }


        [HttpPost("products/import")]
        [Consumes("multipart/form-data")]
        [ApiExplorerSettings(GroupName = "internal")]
        [EndpointSummary("BO import product data")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "BUSINESS_OWNER")]
        public async Task<IActionResult> Import(
        [FromForm] ImportProductRequest request,
        CancellationToken ct)
        {
            var result = await _mediator.Send(new ImportProductsCommand(request.File), ct);

            return FromResult(result);
        }
    }
}
