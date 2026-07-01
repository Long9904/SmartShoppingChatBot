using MassTransit;
using MediatR;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreateEmbed;

namespace SmartShoppingChatBot.Application.Consumers
{
    public class ProductEmbedInQdrant : IConsumer<ProductCreateEvent>
    {

        private IMediator _mediator;

        public ProductEmbedInQdrant(IMediator mediator)
        {
            _mediator = mediator;
        }

        public Task Consume(ConsumeContext<ProductCreateEvent> context)
        {
            var command = new ProductEmbedCommand
            {
                ProductId = context.Message.ProductId,
                QdrantPointId = context.Message.QdrantPointId
            };

            var result = _mediator.Send(command);

            if (result.Result.IsSuccess)
            {
                return Task.CompletedTask;
            }
            else
            {
                throw new Exception(result.Result.Message);
            }
        }
    }
}
