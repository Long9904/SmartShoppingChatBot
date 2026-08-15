using System.Diagnostics;
using System.Globalization;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessage
{
    public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Result<ConversationResponse>>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IBusinessQuotaRepository _buinessQuotaRepository;
        private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SendMessageCommandHandler> _logger;
        private readonly TimeProvider _time;
        private readonly IKernelChatService _kernelChatService;
        private readonly IProductReferenceCollector _productReferenceCollector;
        private readonly IProductReferenceResolver _productReferenceResolver;
        private readonly IConversationContextService _conversationContextService;
        private readonly IPublishEndpoint _publisher;
        private readonly RedisOptions _options;

        public SendMessageCommandHandler(
            ICustomerRepository customerRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IBusinessQuotaRepository buinessQuotaRepository,
            IUsageQuotaLogRepository usageQuotaLogRepository,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            IOptions<RedisOptions> options,
            ILogger<SendMessageCommandHandler> logger,
            ICurrentUserService currentUserService,
            IProductReferenceCollector productReferenceCollector,
            IProductReferenceResolver productReferenceResolver,
            IConversationContextService conversationContextService,
            IKernelChatService kernelChatService,
            IPublishEndpoint publisher)

        {
            _customerRepository = customerRepository;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _buinessQuotaRepository = buinessQuotaRepository;
            _usageQuotaLogRepository = usageQuotaLogRepository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _time = time;
            _logger = logger;
            _options = options.Value;
            _productReferenceCollector = productReferenceCollector;
            _productReferenceResolver = productReferenceResolver;
            _conversationContextService = conversationContextService;
            _kernelChatService = kernelChatService;
            _publisher = publisher;
        }

        public async Task<Result<ConversationResponse>> Handle(
            SendMessageCommand request,
            CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();

            if (!business.IsSuccess || business.Data is null)
                return Result<ConversationResponse>.Failure(
                    statusCode: business.StatusCode,
                    message: business.Message,
                    messageCode: business.MessageCode);

            var customer = await GetOrCreateCustomerAsync(request.ExternalCustomerId, business.Data!);

            var businessCurrentQuota = await _buinessQuotaRepository.GetCurrentBusinessQuota(business.Data.Id);
            if (businessCurrentQuota is null)
                return Result<ConversationResponse>.Failure(
                    statusCode: 404,
                    message: "Business quota not found",
                    messageCode: BusinessQuotaMessageCode.NotFound);

            if (businessCurrentQuota.UsedMessages > businessCurrentQuota.MessageLimit)
            {
                return Result<ConversationResponse>.Failure(
                    statusCode: 404,
                    message: "Doanh nghiệp đã đạt đến giới hạn sử dụng hiện tại",
                    messageCode: BusinessQuotaMessageCode.TokenLimitExceeded);
            }

            if (businessCurrentQuota.UsedTokens > businessCurrentQuota.TokenLimit)
            {
                return Result<ConversationResponse>.Failure(
                    statusCode: 429,
                    message: "Doanh nghiệp đã đạt đến giới hạn sử dụng hiện tại",
                    messageCode: BusinessQuotaMessageCode.TokenLimitExceeded);
            }

            Conversation? conversation;
            try
            {
                var createTime = _time.GetUtcNow();

                // 1. Create new or load conversation

                if (string.IsNullOrEmpty(request.ConversationId) || request.ConversationId == null)
                {
                    var title = request.Message.Length > 30
                        ? request.Message.Substring(0, 30) + "..."
                        : request.Message;
                    conversation = new()
                    {
                        Title = title,
                        BusinessId = business.Data!.Id,
                        CreateAt = createTime,
                        CustomerId = customer.Data!.Id,
                        Id = ObjectId.GenerateNewId(),
                        Status = ConversationStatus.Active
                    };
                    await _conversationRepository.AddAsync(conversation);
                }
                else
                {
                    if (!ObjectId.TryParse(request.ConversationId, out var conversationId))
                    {
                        return Result<ConversationResponse>.Failure(
                            400,
                            "Invalid conversation ID.",
                            messageCode: ConversationMessageCode.InvalidId);
                    }

                    conversation = await _conversationRepository.FindAsync(x =>
                        x.Id == conversationId
                        && x.BusinessId == business.Data.Id
                        && x.CustomerId == customer.Data!.Id);

                    if (conversation == null) return Result<ConversationResponse>
                            .Failure(404, "Conversation not found", null, ConversationMessageCode.NotFound);

                    conversation.LastMessageAt = createTime;
                    await _conversationRepository.UpdateAsync(conversation);
                }


                var userMessage = new Message
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = business.Data!.Id,
                    ConversationId = conversation.Id,
                    Content = request.Message,
                    ContentType = ContentTypeEnum.Text,
                    CreatedAt = createTime,
                    SenderType = SenderTypeEnum.Customer,
                    Status = MessageStatus.Sent,
                };

                await _messageRepository.AddAsync(userMessage);

                // 2. Take conversation context from Redis or load it from the database
                var sw = Stopwatch.StartNew();

                var conversationContext = await _conversationContextService.GetOrLoadAsyncConversationCache(
                    conversation.Id.ToString(), cancellationToken);


                // 3. Send req to semantic kernel + old context summary
                KernelChatRequest req = new()
                {
                    ConversationContextCache = conversationContext,
                    Business = business.Data,
                    UserMessage = request.Message,
                };

                _productReferenceCollector.Reset();

                var sematicKernelResponse = await _kernelChatService.ChatAsync(req);


                if (!sematicKernelResponse.IsSuccess)
                {
                    await _unitOfWork.RollBackAsync(cancellationToken);

                    return Result<ConversationResponse>
                        .Success(null, 200, "Xin lỗi, hiện mình chưa thể trả lời câu hỏi này. Bạn vui lòng thử lại hoặc liên hệ nhân viên hỗ trợ nhé.", "MG_SERVER_200");
                }


                var kernelResult = sematicKernelResponse.Data!;
                var responseTime = _time.GetUtcNow();

                // 4. Build AI response
                var aiMessage = new Message
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = business.Data!.Id,
                    ConversationId = conversation.Id,
                    Content = kernelResult.Answer,
                    ContentType = ContentTypeEnum.Text,
                    CreatedAt = responseTime,
                    SenderType = SenderTypeEnum.ChatBot,
                    Status = MessageStatus.Sent,
                };

                var cacheProducts = _productReferenceCollector.GetProducts();
                var cachedProductDetails = cacheProducts
                    .Select(product => product.ToProductResponseV2())
                    .ToList();

                var productById = BuildAvailableProductReferences(
                    cacheProducts,
                    conversationContext);

                var selectedProductIds = kernelResult.SelectedProductIds
                    .Where(productId => !string.IsNullOrWhiteSpace(productId))
                    .Select(productId => productId.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var selectedProductReferenceCandidates = selectedProductIds
                    .Where(productById.ContainsKey)
                    .Select((productId, index) =>
                    {
                        var product = productById[productId];

                        return new CachedProductReference
                        {
                            DisplayOrder = index + 1,
                            DisplayName = product.DisplayName,
                            ProductId = product.ProductId,
                            ExternalProductId = product.ExternalProductId,
                        };
                    })
                    .ToList();

                var responseProductIds = selectedProductReferenceCandidates
                    .Select(product => product.ProductId)
                    .ToList();

                var responseProductById = await _productReferenceResolver.ResolveAsync(
                    business.Data.Id,
                    responseProductIds,
                    cachedProductDetails,
                    cancellationToken);

                var productListResponse = _productReferenceResolver
                    .GetInOrder(responseProductIds, responseProductById)
                    .Select(MessageProductResponse.FromProduct)
                    .ToList();

                var resolvedProductById = productListResponse
                    .ToDictionary(product => product.ProductId, StringComparer.OrdinalIgnoreCase);

                var selectedProductReferences = selectedProductReferenceCandidates
                    .Select((product, index) => new CachedProductReference
                    {
                        DisplayOrder = index + 1,
                        ProductId = product.ProductId,
                        ExternalProductId = resolvedProductById.TryGetValue(product.ProductId, out var resolvedProduct)
                            ? NormalizeExternalProductId(resolvedProduct.ExternalId)
                            : NormalizeExternalProductId(product.ExternalProductId),
                        DisplayName = product.DisplayName
                    })
                    .ToList();

                aiMessage.CacheProductReference = selectedProductReferences
                    .Select(product =>
                    {
                        return new ProductReference
                        {
                            ProductId = product.ProductId,
                            ExternalProductId = product.ExternalProductId,
                            DisplayName = product.DisplayName
                        };
                    })
                    .ToList();



                var gptCredits = kernelResult.InputTokens + kernelResult.OutputTokens * 6;
                var usageLog = new UsageQuotaLog
                {
                    BillableTokens = gptCredits,
                    OutputTokens = kernelResult.OutputTokens,
                    InputTokens = kernelResult.InputTokens,
                    CreatedAt = _time.GetUtcNow(),
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = business.Data.Id,
                    BusinessQuotaId = businessCurrentQuota.Id,
                    MessageUsed = 1,
                    SourceId = aiMessage.Id,
                    SourceType = SourceTypeEnum.Chat
                };

                aiMessage.SummaryContent = kernelResult.AISummaryContent ?? "";

                await _messageRepository.AddAsync(aiMessage);

                businessCurrentQuota.UsedMessages += 1;
                businessCurrentQuota.UsedTokens += gptCredits;

                conversation.Summary = kernelResult.Summary;
                conversation.SummaryUpdatedAt = responseTime;
                conversation.LastMessageAt = responseTime;


                await _buinessQuotaRepository.UpdateAsync(businessCurrentQuota);
                await _usageQuotaLogRepository.AddAsync(usageLog);

                sw.Stop();
                _logger.LogInformation("Total time AI reposne: {time} ms", sw.ElapsedMilliseconds);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                await PublishAnalyticsEventsAsync(
                    business.Data.Id,
                    customer.Data!.Id,
                    conversation.Id,
                    userMessage.Id,
                    aiMessage,
                    request.Message,
                    kernelResult,
                    cacheProducts,
                    productListResponse,
                    sw.ElapsedMilliseconds,
                    cancellationToken);

                // 5. Build turn cache to save orther context to redis
                var turn = new CachedConversationTurn
                {
                    TurnId = userMessage.Id.ToString(),

                    UserMessage = new()
                    {
                        Content = userMessage.Content,
                        MessageId = userMessage.Id.ToString()
                    },

                    AssistantMessage = new()
                    {
                        MessageId = aiMessage.Id.ToString(),
                        Content = aiMessage.SummaryContent ?? "",
                        ProductReferences = selectedProductReferences
                    }
                };

                conversationContext.RecentTurns.Add(turn);
                conversationContext.Summary = kernelResult.Summary;

                // 5. sliding window for maximum RecentTurnLimit for new context

                if (conversationContext.RecentTurns.Count > _options.RecentTurnLimit)
                {
                    var overFlowCount = conversationContext.RecentTurns.Count - _options.RecentTurnLimit;

                    conversationContext.RecentTurns.RemoveRange(0, overFlowCount);
                }

                // 6. Save turn mới vào reids
                await _conversationContextService.SaveConversationCacheAsync(conversationContext, cancellationToken);


                var response = new ConversationResponse
                {
                    ConversationId = conversation.Id.ToString(),
                    ConversationTitle = conversation.Title,
                    MessageResponse = aiMessage.Content,
                    ProductReferences = productListResponse
                };

                return Result<ConversationResponse>.Success(response, 200, "Kernel response success", ConversationMessageCode.Success);

            }
            catch (Exception ex)
            {
                await _unitOfWork.RollBackAsync(cancellationToken);
                _logger.LogError(ex, "Error when saving user message or kernel response");

                return Result<ConversationResponse>
                    .Failure(500, "Error when saving user message or kernel response", null, "MG_SERVER_500");
            }

        }

        private static Dictionary<string, CachedProductReference> BuildAvailableProductReferences(
            IEnumerable<ProductResponseV3> currentProducts,
            ConversationContextCache conversationContext)
        {

            // Tạo 1 cái distionary bằng productId + tham chiếu của nó
            var productById = new Dictionary<string, CachedProductReference>(StringComparer.OrdinalIgnoreCase);

            // Lấy tất cả tham chiếu ở trên hisotry
            var contextProductReferences = conversationContext.RecentTurns
                .SelectMany(turn => turn.AssistantMessage?.ProductReferences ?? []);

            // ADd tất cả tham chiếu vào distionary
            foreach (var product in contextProductReferences)
            {
                if (string.IsNullOrWhiteSpace(product.ProductId))
                {
                    continue;
                }

                var productId = product.ProductId.Trim();

                productById.TryAdd(productId, new CachedProductReference
                {
                    ProductId = productId,
                    ExternalProductId = product.ExternalProductId,
                    DisplayName = product.DisplayName,
                    DisplayOrder = product.DisplayOrder,
                });
            }
            // Thêm cache product vào distionary
            foreach (var product in currentProducts)
            {
                if (string.IsNullOrWhiteSpace(product.ProductId))
                {
                    continue;
                }

                var productId = product.ProductId.Trim();

                productById[productId] = new CachedProductReference
                {
                    ProductId = productId,
                    ExternalProductId = product.ExternalProductId,
                    DisplayName = product.Name,
                };
            }

            return productById;
        }

        private static string? NormalizeExternalProductId(string? externalProductId)
        {
            return string.IsNullOrWhiteSpace(externalProductId)
                ? null
                : externalProductId.Trim();
        }

        private async Task PublishAnalyticsEventsAsync(
            ObjectId businessId,
            ObjectId customerId,
            ObjectId conversationId,
            ObjectId queryMessageId,
            Message aiMessage,
            string rawQuery,
            KernelChatResult kernelResult,
            IReadOnlyCollection<ProductResponseV3> retrievedProducts,
            IReadOnlyCollection<MessageProductResponse> selectedProducts,
            long retrievalLatency,
            CancellationToken cancellationToken)
        {
            var searchProducts = retrievedProducts
                .Where(product => ObjectId.TryParse(product.ProductId, out _))
                .DistinctBy(product => product.ProductId, StringComparer.OrdinalIgnoreCase)
                .Select(product => new SearchQueryProductSnapshot
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    Price = ParsePrice(product.Price),
                    Category = product.Category,
                    ProductScore = Math.Round(product.Score, 2)
                })
                .ToList();

            await _publisher.Publish(new SearchQueryLogRequestedEvent
            {
                BusinessId = businessId.ToString(),
                ConversationId = conversationId.ToString(),
                MessageId = queryMessageId.ToString(),
                UserRawQuery = rawQuery,
                TrendKeywords = kernelResult.TrendKeywords?
                    .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                    .Select(keyword => keyword.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList(),
                InteractionType = string.IsNullOrWhiteSpace(kernelResult.InteractionType)
                    ? null
                    : kernelResult.InteractionType.Trim(),
                CreatedAt = aiMessage.CreatedAt,
                RetrievalLatency = retrievalLatency,
                TopKResult = selectedProducts.Count,
                ProductResults = searchProducts
            }, cancellationToken);

            if (!ContainsMarkdownTable(aiMessage.Content))
            {
                return;
            }

            var comparedProductIds = kernelResult.ComparedProductIds
                .Concat(kernelResult.SelectedProductIds)
                .Where(productId => ObjectId.TryParse(productId?.Trim(), out _))
                .Select(productId => productId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (comparedProductIds.Count < 2)
            {
                return;
            }

            var comparedProductById = await _productReferenceResolver.ResolveAsync(
                businessId,
                comparedProductIds,
                retrievedProducts.Select(product => product.ToProductResponseV2()),
                cancellationToken);
            var comparedProducts = _productReferenceResolver
                .GetInOrder(comparedProductIds, comparedProductById)
                .Take(10)
                .ToList();

            if (comparedProducts.Count < 2)
            {
                return;
            }

            await _publisher.Publish(new ProductComparisonDetectedEvent
            {
                BusinessId = businessId.ToString(),
                ConversationId = conversationId.ToString(),
                MessageId = aiMessage.Id.ToString(),
                CustomerId = customerId.ToString(),
                CreatedAt = aiMessage.CreatedAt,
                Title = ExtractMarkdownTableTitle(aiMessage.Content),
                Summary = aiMessage.SummaryContent,
                Products = comparedProducts.Select(product => new ComparedProductSnapshot
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    Price = ParsePrice(product.Price),
                    Category = product.Category
                }).ToList()
            }, cancellationToken);
        }

        private static bool ContainsMarkdownTable(string content)
        {
            return FindMarkdownTableHeaderIndex(content) >= 0;
        }

        // Hơi khó hiểu, tôi cũng thế, đừng xóa làm chi

        private static int FindMarkdownTableHeaderIndex(string content)
        {
            // tách dòng và chuẩn hóa
            var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

            for (var index = 0; index < lines.Length - 1; index++)
            {
                // Vì mỗi table ít nhất phải 2 dấu |
                if (lines[index].Count(character => character == '|') < 2)
                {
                    continue;
                }

                var separatorCells = lines[index + 1]
                    .Trim()
                    .Trim('|')
                    .Split('|', StringSplitOptions.TrimEntries);
                // Ví dụ: "| --- | :---: |" → sau xử lý còn ["---", ":---:"].

                if (separatorCells.Length >= 2
                    && separatorCells.All(cell =>
                    {
                        var value = cell.Trim().TrimStart(':').TrimEnd(':');
                        return value.Length >= 3 && value.All(character => character == '-');
                    }))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string? ExtractMarkdownTableTitle(string content)
        {
            var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var tableIndex = FindMarkdownTableHeaderIndex(content);
            if (tableIndex <= 0)
            {
                return null;
            }

            return lines
                .Take(tableIndex)
                .Reverse()
                .Select(line => line.Trim().TrimStart('#').Trim())
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        }

        private static decimal ParsePrice(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var numericValue = new string(value
                .Trim()
                .TakeWhile(character => char.IsDigit(character)
                    || character is ' ' or '.' or ',' or '-' or '+')
                .ToArray())
                .Trim();

            return decimal.TryParse(numericValue, NumberStyles.Number, CultureInfo.CurrentCulture, out var price)
                || decimal.TryParse(numericValue, NumberStyles.Number, CultureInfo.InvariantCulture, out price)
                    ? price
                    : 0;
        }

        private async Task<Result<Customer>> GetOrCreateCustomerAsync(
            string externalCustomerId,
            Business business)
        {

            var customer = await _customerRepository.FindAsync(x =>
            x.CustomerExternalId == externalCustomerId
            && x.BusinessId == business.Id);

            if (customer != null)
                return Result<Customer>.Success(
                    data: customer,
                    message: "Get customer success",
                    messageCode: CustomerMessageCode.Success);

            var newCustomer = new Customer
            {
                CustomerExternalId = externalCustomerId,
                BusinessId = business.Id,
                Status = CustomerStatus.Active,
                CreatedAt = _time.GetUtcNow(),
                Id = ObjectId.GenerateNewId(),
            };

            await _customerRepository.AddAsync(newCustomer);
            await _unitOfWork.SaveChangesAsync();

            return Result<Customer>.Success(
                data: newCustomer,
                message: "Create customer success",
                messageCode: CustomerMessageCode.Create);
        }
    }
}
