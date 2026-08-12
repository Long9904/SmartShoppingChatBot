using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument
{
    public class EmbeddingDocumentCommand : IRequest<Result<GeminiResponse<string>>>
    {
        public string BusinessId { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
    }
}
