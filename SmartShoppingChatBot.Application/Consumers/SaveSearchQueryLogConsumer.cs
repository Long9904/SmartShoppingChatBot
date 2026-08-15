using MassTransit;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Consumers;

public sealed class SaveSearchQueryLogConsumer(
    ISearchQueryLogRepository repository,
    IUnitOfWork unitOfWork) : IConsumer<SearchQueryLogRequestedEvent>
{
    public async Task Consume(ConsumeContext<SearchQueryLogRequestedEvent> context)
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

        var log = new SearchQueryLog
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = ObjectId.Parse(message.ConversationId),
            MessageId = messageId,
            UserRawQuery = message.UserRawQuery,
            TrendKeywords = message.TrendKeywords?.ToList(),
            InteractionType = message.InteractionType,
            ZeroResult = message.ProductResults.Count == 0,
            ResultCountNumber = message.ProductResults.Count,
            TopKResult = message.TopKResult,
            CreatedAt = message.CreatedAt,
            RetrievalLatency = message.RetrievalLatency,
            HitRateScore = message.ProductResults.Count == 0
                ? null
                : message.ProductResults.Max(product => product.ProductScore),
            ProductResults = message.ProductResults.Select(product => new ProductLogSnapshot
            {
                ProductId = ObjectId.Parse(product.ProductId),
                ProductName = product.ProductName,
                Price = product.Price,
                Category = product.Category,
                ProductScore = product.ProductScore
            }).ToList()
        };

        await repository.AddAsync(log);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
