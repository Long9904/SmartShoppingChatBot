using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ConversationContextService : IConversationContextService
    {
        private readonly IConversationContextCacheService _cacheService;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly RedisOptions _options;
        private readonly ILogger<ConversationContextService> _logger;

        public ConversationContextService(
            IConversationContextCacheService cacheService,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IOptions<RedisOptions> options,
            ILogger<ConversationContextService> logger)
        {
            _cacheService = cacheService;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ConversationContextCache> GetOrLoadAsyncConversationCache(
            string conversationId,
            CancellationToken ct)
        {

            if (!ObjectId.TryParse(conversationId, out var conversationObjectId))
            {
                throw new ArgumentException(
                    "Invalid conversationId.",
                    nameof(conversationId));
            }


            // 1. Lấy context từ redis
            var cachedContext = await _cacheService.GetAsync(conversationId, ct);
            if (cachedContext is not null) return cachedContext;


            // 2. Lấy context từ db nếu reis null
            var conversationData = await _conversationRepository.FindAsync(c => c.Id == conversationObjectId);
            if (conversationData is null) return CreateEmptyContext(conversationId);



            // 3. Lấy message mới nhất.
            // 8 turn thường là 16 message.
            // Lấy dư một ít để phòng trường hợp turn chưa hoàn thành.

            var messageLimit = _options.RecentTurnLimit * 2 + 2;

            var query = _messageRepository
                .AsQueryable()
                .Where(message =>
                    message.ConversationId == conversationObjectId)
                .OrderByDescending(message => message.CreatedAt);

            var result = await _messageRepository.PaginatedListAsync(
                query,
                index: 0,
                pageSize: messageLimit);

            var messages = result?.Items?.ToList() ?? [];

            // 8 turn tin mới nhất = 16 message AI và User: tin 20, tin 19,.. tin 13. Đảo ngược lại để xử lí từ tin 13 trước
            messages.Reverse();

            // 4. Chuyển danh sách message thành turn.
            var recentTurns = BuildRecentTurns(messages)
                .TakeLast(_options.RecentTurnLimit)
                .ToList();

            var context = new ConversationContextCache
            {
                ConversationId = conversationId,
                Summary = conversationData.Summary ?? string.Empty,
                RecentTurns = recentTurns,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _cacheService.SetAsync(context, ct);

            _logger.LogInformation(
            "Conversation context {ConversationId} was restored from database",
            conversationId);

            return context;
        }

        public Task InvalidateConversationCacheAsync(
            string conversationId,
            CancellationToken ct)
        {
            return _cacheService.RemoveAsync(conversationId, ct);
        }

        public Task SaveConversationCacheAsync(
            ConversationContextCache conversationContextCache,
            CancellationToken ct)
        {
            return _cacheService.SetAsync(conversationContextCache, ct);
        }

        private static ConversationContextCache CreateEmptyContext(string conversationId)
        {
            return new ConversationContextCache
            {
                ConversationId = conversationId,
                Summary = string.Empty,
                RecentTurns = [],
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }



        private static List<CachedConversationTurn> BuildRecentTurns(IReadOnlyCollection<Message> messages)
        {
            var turns = new List<CachedConversationTurn>();

            CachedConversationTurn? currentTurn = null;

            foreach (var message in messages)
            {
                if (message.SenderType == SenderTypeEnum.Customer)
                {
                    currentTurn = new CachedConversationTurn
                    {
                        TurnId = message.Id.ToString(),
                        UserMessage = new CachedUserMessage
                        {
                            MessageId = message.Id.ToString(),
                            Content = message.Content,
                            CreatedAt = message.CreatedAt
                        },
                        AssistantMessage = null,
                        CreatedAt = message.CreatedAt
                    };

                    turns.Add(currentTurn);

                    continue;
                }

                if (message.SenderType != SenderTypeEnum.ChatBot)
                {
                    continue;
                }

                // Assistant message không có user message tương ứng.
                // Có thể xảy ra với dữ liệu cũ hoặc message hệ thống.
                if (currentTurn is null || currentTurn.AssistantMessage is not null)
                {
                    continue;
                }

                currentTurn.AssistantMessage = new CachedAssistantMessage
                {
                    MessageId = message.Id.ToString(),
                    Content = message.Content,
                    ProductReferences = BuildProductReferences(message),
                    CreatedAt = message.CreatedAt
                };
            }

            return turns;
        }


        private static List<CachedProductReference> BuildProductReferences(Message message)
        {
            if (message.CacheProductReference is null || message.CacheProductReference.Count == 0)
            {
                return [];
            }

            return message.CacheProductReference
                .Select((product, index) =>
                    new CachedProductReference
                    {
                        ProductId = product.ProductId,
                        DisplayName = product.DisplayName ?? "",
                    }).ToList();
        }
    }
}
