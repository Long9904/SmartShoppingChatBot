using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

// Keep the existing Redis and message formats at the storage boundary.
// Inside V3 every product reference uses ProductReferenceV3.
public sealed class ConversationContextServiceV3(
    IConversationContextService storage,
    ICurrentUserService currentUser,
    IProductReferenceResolverV3 resolver) : IConversationContextServiceV3
{
    public async Task<ConversationContextCacheV3> GetOrLoadAsyncConversationCache(string conversationId, CancellationToken ct)
    {
        var business = await currentUser.GetBusiness();
        if (!business.IsSuccess || business.Data is null)
            throw new InvalidOperationException("Business context is unavailable.");
        var saved = await storage.GetOrLoadAsyncConversationCache(conversationId, ct);
        var ids = saved.RecentTurns.SelectMany(t => t.AssistantMessage?.ProductReferences ?? [])
            .Select(p => p.ProductId);
        // Refresh old references so the AI has real attributes for styling and price requests.
        var products = await resolver.ResolveAsync(business.Data.Id, ids, cancellationToken: ct);
        return new ConversationContextCacheV3
        {
            ConversationId = saved.ConversationId, Summary = saved.Summary,
            RecentTurns = saved.RecentTurns.Select(t => new CachedConversationTurnV3
            {
                TurnId = t.TurnId,
                UserMessage = new() { MessageId = t.UserMessage.MessageId, Content = t.UserMessage.Content },
                AssistantMessage = t.AssistantMessage is null ? null : new CachedAssistantMessageV3
                {
                    MessageId = t.AssistantMessage.MessageId, Content = t.AssistantMessage.Content,
                    ProductReferences = t.AssistantMessage.ProductReferences.Select(p =>
                    {
                        var reference = products.TryGetValue(p.ProductId, out var found) ? found.Copy()
                            : new ProductReferenceV3 { ProductId = p.ProductId, Name = p.DisplayName, ExternalProductId = p.ExternalProductId };
                        reference.DisplayOrder = p.DisplayOrder;
                        return reference;
                    }).ToList()
                }
            }).ToList()
        };
    }

    public Task SaveConversationCacheAsync(ConversationContextCacheV3 context, CancellationToken ct) =>
        storage.SaveConversationCacheAsync(new ConversationContextCache
        {
            ConversationId = context.ConversationId, Summary = context.Summary,
            RecentTurns = context.RecentTurns.Select(t => new CachedConversationTurn
            {
                TurnId = t.TurnId,
                UserMessage = new() { MessageId = t.UserMessage.MessageId, Content = t.UserMessage.Content },
                AssistantMessage = t.AssistantMessage is null ? null : new CachedAssistantMessage
                {
                    MessageId = t.AssistantMessage.MessageId, Content = t.AssistantMessage.Content,
                    ProductReferences = t.AssistantMessage.ProductReferences.Select(p => new CachedProductReference
                    {
                        ProductId = p.ProductId, ExternalProductId = p.ExternalProductId,
                        DisplayName = p.Name, DisplayOrder = p.DisplayOrder
                    }).ToList()
                }
            }).ToList()
        }, ct);
}
