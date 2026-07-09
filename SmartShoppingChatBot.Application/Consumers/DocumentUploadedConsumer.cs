using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument;

namespace SmartShoppingChatBot.Application.Consumers
{
    public class DocumentUploadedConsumer : IConsumer<DocumentUploadedEvent>
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DocumentUploadedConsumer> _logger;

        public DocumentUploadedConsumer(IMediator mediator, ILogger<DocumentUploadedConsumer> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<DocumentUploadedEvent> context)
        {
            var command = new EmbeddingDocumentCommand
            {
                DocumentId = context.Message.DocumentId,
                BusinessId = context.Message.BusinessId
            };
            var result = await _mediator.Send(command, context.CancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Document embedding failed for DocumentId {DocumentId}: {Message}",context.Message.DocumentId,result.Message);
                return;
            }
        }
    }
}
