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

        public async Task Consume(ConsumeContext<ProductCreateEvent> context)
        {
            var command = new ProductEmbedCommand
            {
                ProductId = context.Message.ProductId,
                QdrantPointId = context.Message.QdrantPointId
            };

            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(12), context.CancellationToken);

        }
    }
}
