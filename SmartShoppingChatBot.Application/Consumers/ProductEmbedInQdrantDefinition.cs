using MassTransit;

namespace SmartShoppingChatBot.Application.Consumers
{
    public class ProductEmbedInQdrantDefinition
        : ConsumerDefinition<ProductEmbedInQdrant>
    {
        public ProductEmbedInQdrantDefinition()
        {
            EndpointName = "product-embedding-requested";
            ConcurrentMessageLimit = 1;
        }

        protected override void ConfigureConsumer(
            IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<ProductEmbedInQdrant> consumerConfigurator,
            IRegistrationContext context)
        {
            endpointConfigurator.PrefetchCount = 1;
        }
    }
}
