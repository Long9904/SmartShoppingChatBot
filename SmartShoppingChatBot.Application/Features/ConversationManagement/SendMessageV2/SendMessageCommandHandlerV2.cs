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

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessageV2
{
    public class SendMessageCommandHandlerV2 : IRequestHandler<SendMessageCommandV2, Result<ConversationResponse>>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IBusinessQuotaRepository _buinessQuotaRepository;
        private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SendMessageCommandHandlerV2> _logger;
        private readonly TimeProvider _time;
        private readonly IKernelChatService _kernelChatService;
        private readonly IProductReferenceCollectorV2 _productReferenceCollectorV2;
        private readonly IConversationContextService _conversationContextService;
        private readonly IPublishEndpoint _publisher;
        private readonly RedisOptions _options;

        public SendMessageCommandHandlerV2(
            ICustomerRepository customerRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IBusinessQuotaRepository buinessQuotaRepository,
            IUsageQuotaLogRepository usageQuotaLogRepository,
            IProductRepository productRepository,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            IOptions<RedisOptions> options,
            ILogger<SendMessageCommandHandlerV2> logger,
            ICurrentUserService currentUserService,
            IProductReferenceCollectorV2 productReferenceCollectorV2,
            IConversationContextService conversationContextService,
            IKernelChatService kernelChatService,
            IPublishEndpoint publisher)

        {
            _customerRepository = customerRepository;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _productRepository = productRepository;
            _buinessQuotaRepository = buinessQuotaRepository;
            _usageQuotaLogRepository = usageQuotaLogRepository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _time = time;
            _logger = logger;
            _options = options.Value;
            _productReferenceCollectorV2 = productReferenceCollectorV2;
            _conversationContextService = conversationContextService;
            _kernelChatService = kernelChatService;
            _publisher = publisher;
        }

        public async Task<Result<ConversationResponse>> Handle(
            SendMessageCommandV2 request,
            CancellationToken cancellationToken)
        {
            // 1. Business validation
            var (business, businessCurrentQuota) = await BusinessValidation();


            if (!business.IsSuccess || business.Data is null || businessCurrentQuota is null)
            {
                return Result<ConversationResponse>
                    .Failure(business.StatusCode, business.Message, null, business.MessageCode);
            }

            // 2. Customer data
            var customer = await GetOrCreateCustomerAsync(request.ExternalCustomerId, business.Data!);

            // 3. Conversation process
            Conversation? conversation;
            try
            {
                var createTime = _time.GetUtcNow();

                // 4. Create or get new conversation
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

                // 5. Create new user message
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

                var sw = Stopwatch.StartNew();

                // 6. Load conversation context history
                var conversationContext = await _conversationContextService
                    .GetOrLoadAsyncConversationCache(conversation.Id.ToString(), cancellationToken);

                // 7. Send to Kernel to process it's plugins
                KernelChatRequest req = new()
                {
                    ConversationContextCache = conversationContext,
                    Business = business.Data,
                    UserMessage = request.Message,
                };

                _productReferenceCollectorV2.ResetFromV2();

                var sematicKernelResponse = await _kernelChatService.ChatAsync(req);


                if (!sematicKernelResponse.IsSuccess)
                {
                    await _unitOfWork.RollBackAsync(cancellationToken);

                    return Result<ConversationResponse>
                        .Success(null, 200, "Xin lỗi, hiện mình chưa thể trả lời câu hỏi này. Bạn vui lòng thử lại hoặc liên hệ nhân viên hỗ trợ nhé.", "MG_SERVER_200");
                }


                var kernelResult = sematicKernelResponse.Data!;
                var responseTime = _time.GetUtcNow();

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

                // 8. Build Product reference for save, history context anh event publish
                var cacheProducts = _productReferenceCollectorV2.GetProductsFromV2();

                var knownProductIds = conversationContext.RecentTurns
                    .SelectMany(turn => turn.AssistantMessage?.ProductReferences ?? [])
                    .Select(product => product.ProductId)
                    .Concat(cacheProducts.Select(product => product.ProductId))
                    .Where(productId => !string.IsNullOrWhiteSpace(productId))
                    .Select(productId => productId.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var responseProductIds = kernelResult.SelectedProductIds
                    .Where(productId => !string.IsNullOrWhiteSpace(productId))
                    .Select(productId => productId.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(knownProductIds.Contains)
                    .ToList();

                var selectedProducts = await ResolveInOrderAsync(
                    business.Data.Id, responseProductIds, cacheProducts, cancellationToken);

                var selectedProductReferences = selectedProducts
                    .Select((product, index) => new CachedProductReference
                    {
                        DisplayOrder = index + 1,
                        ProductId = product.ProductId,
                        ExternalProductId = NormalizeExternalProductId(product.ExternalProductId),
                        DisplayName = product.Name
                    })
                    .ToList();

                var productListResponse = selectedProducts
                    .Select(product => new MessageProductResponse
                    {
                        ProductId = product.ProductId,
                        ExternalId = product.ExternalProductId ?? string.Empty,
                        ExternalProductUrl = product.ExternalProductUrl,
                        Name = product.Name,
                        Price = product.Price?.ToString(CultureInfo.InvariantCulture),
                        StockQuantity = product.StockQuantity
                    })
                    .ToList();

                aiMessage.CacheProductReference = selectedProductReferences
                    .Select(product => new ProductReference
                    {
                        ProductId = product.ProductId,
                        ExternalProductId = product.ExternalProductId,
                        DisplayName = product.DisplayName
                    })
                    .ToList();

                // 9. Token credit base is 0.75/1M token
                // gpt 5.4 mini Input 0.75/1M - Output: 4.5/1M
                var gptCredits = kernelResult.InputTokens + kernelResult.OutputTokens * 6;
                var usageLog = new UsageQuotaLog
                {
                    BillableTokens = gptCredits,
                    OutputTokens = kernelResult.OutputTokens * 6,
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

                // 10. Publist event
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

                // 11. Add data into history context and remove if out of max turn
                conversationContext.RecentTurns.Add(new CachedConversationTurn
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
                });
                conversationContext.Summary = kernelResult.Summary;

                if (conversationContext.RecentTurns.Count > _options.RecentTurnLimit)
                {
                    var overFlowCount = conversationContext.RecentTurns.Count - _options.RecentTurnLimit;
                    conversationContext.RecentTurns.RemoveRange(0, overFlowCount);
                }

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
                    .Failure(500, "Server error when use AI chat", null, "MG_SERVER_500");
            }

        }


        private async Task<IReadOnlyList<ProductReferenceV2>> ResolveInOrderAsync(
            ObjectId businessId,
            IEnumerable<string> selectedIds,
            IEnumerable<ProductReferenceV2> knownProducts,
            CancellationToken cancellationToken = default)
        {
            var listIdWasSelectedByAI = selectedIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var productsById = await ResolveAsync(
                businessId,
                listIdWasSelectedByAI,
                knownProducts,
                cancellationToken);

            return GetListProductInOrder(listIdWasSelectedByAI, productsById);
        }

        private async Task<IReadOnlyDictionary<string, ProductReferenceV2>> ResolveAsync(
            ObjectId businessId,
            IEnumerable<string> listIdWasSelectedByAI,
            IEnumerable<ProductReferenceV2>? knownProducts = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = listIdWasSelectedByAI.Where(id => ObjectId.TryParse(id, out _))
                .Select(id => ObjectId.Parse(id).ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, ProductReferenceV2>(StringComparer.OrdinalIgnoreCase);

            foreach (var product in knownProducts ?? [])
            {
                if (ids.Contains(product.ProductId)) result[product.ProductId] = product.Copy();
            }

            var missing = ids.Where(id => !result.ContainsKey(id)).Select(ObjectId.Parse).ToList();

            if (missing.Count > 0)
            {
                var found = await _productRepository.FindAllAsync(p =>
                    missing.Contains(p.Id)
                    && p.BusinessId == businessId
                    && p.Status == ProductStatus.Active);

                cancellationToken.ThrowIfCancellationRequested();

                foreach (var product in found)
                {
                    result[product.Id.ToString()] = ProductReferenceV2.FromProduct(product);
                }
            }
            return result;
        }

        private IReadOnlyList<ProductReferenceV2> GetListProductInOrder(
            IEnumerable<string> listIdWasSelectedByAI,
            IReadOnlyDictionary<string, ProductReferenceV2> products)

            => listIdWasSelectedByAI
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Where(products.ContainsKey)
            .Select(id => products[id].Copy()).ToList();

        private static string? NormalizeExternalProductId(string? externalProductId)
            => string.IsNullOrWhiteSpace(externalProductId) ? null : externalProductId.Trim();


        private async Task PublishAnalyticsEventsAsync(
            ObjectId businessId,
            ObjectId customerId,
            ObjectId conversationId,
            ObjectId queryMessageId,
            Message aiMessage,
            string rawQuery,
            KernelChatResult kernelResult,
            IReadOnlyCollection<ProductReferenceV2> retrievedProducts,
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
                    Price = product.Price ?? 0,
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

            var comparedProducts = (await ResolveInOrderAsync(
                    businessId, comparedProductIds, retrievedProducts, cancellationToken))
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
                    Price = product.Price ?? 0,
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

        private async Task<(Result<Business>, BusinessQuota? businessQuota)> BusinessValidation()
        {
            var business = await _currentUserService.GetBusiness();

            // Authorize
            if (!business.IsSuccess || business.Data is null)
                return (
                    Result<Business>.Failure(
                    statusCode: business.StatusCode,
                    message: business.Message,
                    messageCode: business.MessageCode),
                    null);

            // Quota checking
            var businessCurrentQuota = await _buinessQuotaRepository.GetCurrentBusinessQuota(business.Data.Id);

            if (businessCurrentQuota is null)
                return (
                    Result<Business>.Failure(
                    statusCode: 404,
                    message: "Business quota not found",
                    messageCode: BusinessQuotaMessageCode.NotFound),
                    null);

            if (businessCurrentQuota.UsedMessages > businessCurrentQuota.MessageLimit)
            {
                return (
                    Result<Business>.Failure(
                    statusCode: 404,
                    message: "Doanh nghiệp đã đạt đến giới hạn sử dụng hiện tại",
                    messageCode: BusinessQuotaMessageCode.TokenLimitExceeded),
                    null);
            }

            if (businessCurrentQuota.UsedTokens > businessCurrentQuota.TokenLimit)
            {
                return (
                    Result<Business>.Failure(
                    statusCode: 429,
                    message: "Doanh nghiệp đã đạt đến giới hạn sử dụng hiện tại",
                    messageCode: BusinessQuotaMessageCode.TokenLimitExceeded),
                    null);
            }

            return (Result<Business>.Success(business.Data), businessCurrentQuota);
        }
    }
}
