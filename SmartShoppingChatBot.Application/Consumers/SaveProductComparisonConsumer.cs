using MassTransit;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Consumers;

public sealed class SaveProductComparisonConsumer(
    IProductComparationRepository repository,
    IUnitOfWork unitOfWork) : IConsumer<ProductComparisonDetectedEvent>
{
    public async Task Consume(ConsumeContext<ProductComparisonDetectedEvent> context)
    {
        var message = context.Message;
        var businessId = ObjectId.Parse(message.BusinessId);
        var messageId = ObjectId.Parse(message.MessageId);
        var existing = await repository.FindAsync(item =>
            item.BusinessId == businessId && item.MessageId == messageId);
        if (existing is not null)
        {
            return;
        }

        var comparison = new ProductComparation
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = ObjectId.Parse(message.ConversationId),
            MessageId = messageId,
            CustomerId = ObjectId.Parse(message.CustomerId),
            CreatedAt = message.CreatedAt,
            Title = message.Title,
            Summary = message.Summary,
            RecommendationObjects = message.Products.Select(product => new ProductSnapshot
            {
                ProductId = ObjectId.Parse(product.ProductId),
                ProductName = product.ProductName,
                Price = product.Price,
                Category = product.Category
            }).ToList()
        };

        await repository.AddAsync(comparison);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
