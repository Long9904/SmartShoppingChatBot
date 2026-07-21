
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.DocumentManagement.UploadDocument;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/documents")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DocumentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DocumentController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocuments([FromForm] UploadDocCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<UploadedKnowledgeDocResponse>>.Ok(result.Data!, result.Message));
            }
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<UploadedKnowledgeDocResponse>>.Fail(result.Message!, result.Errors));
        }
    }
}
