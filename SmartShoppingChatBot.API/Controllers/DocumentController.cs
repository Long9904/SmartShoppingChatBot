
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.DocumentManagement.DeleteDocument;
using SmartShoppingChatBot.Application.Features.DocumentManagement.GetAllDocument;
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
        [EndpointDescription("Upload documents to the system")]
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

        [HttpGet]
        [EndpointDescription("Get all documents with optional filters")]
        public async Task<IActionResult> GetDocuments([FromQuery] GetDocumentFilter filter)
        {
            var query = new GetDocumentQuery { Filter = filter };
            var result = await _mediator.Send(query);
            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<DocumentGetResponse>>.Ok(result.Data!, result.Message));
            }
            return StatusCode(result.StatusCode, ApiResponse<BasePaginatedList<DocumentGetResponse>>.Fail(result.Message!, result.Errors));
        }

        [HttpDelete("{documentId}")]
        [EndpointDescription("Delete a document by its ID")]
        public async Task<IActionResult> DeleteDocument([FromRoute] string documentId)
        {
            var command = new DeleteDocumentCommand { DocumentId = documentId.ToString() };
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
            {
                return StatusCode(result.StatusCode, ApiResponse<string>.Ok(result.Data!, result.Message));
            }
            return StatusCode(result.StatusCode, ApiResponse<string>.Fail(result.Message!, result.Errors));
        }
    }
}
