using MassTransit;
using MediatR;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument;

namespace SmartShoppingChatBot.Application.Consumers
{
    public class DocumentUploadedConsumer : IConsumer<DocumentUploadedEvent>
    {
        private readonly IMediator _mediator;

        public DocumentUploadedConsumer(IMediator mediator)
        {
            _mediator = mediator;
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
                throw new Exception(result.Message);
        }
    }
}
