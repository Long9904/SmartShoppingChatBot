using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument
{
    public class EmbeddingDocumentCommand : IRequest<Result<string>>
    {
        public string BusinessId { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
    }
}
