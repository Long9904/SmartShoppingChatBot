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

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessageV3
{
    public class SendMessageCommandHandlerV3 : IRequestHandler<SendMessageCommandV3, Result<ConversationResponse>>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IBusinessQuotaRepository _buinessQuotaRepository;
        private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SendMessageCommandHandlerV3> _logger;
        private readonly TimeProvider _time;
        private readonly IKernelChatServiceV3 _kernelChatService;
        private readonly IProductReferenceCollectorV3 _productReferenceCollector;
        private readonly IProductReferenceResolverV3 _productReferenceResolver;
        private readonly IConversationContextServiceV3 _conversationContextService;
        private readonly IPublishEndpoint _publisher;
        private readonly RedisOptions _options;

        public SendMessageCommandHandlerV3(
            ICustomerRepository customerRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IBusinessQuotaRepository buinessQuotaRepository,
            IUsageQuotaLogRepository usageQuotaLogRepository,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            IOptions<RedisOptions> options,
            ILogger<SendMessageCommandHandlerV3> logger,
            ICurrentUserService currentUserService,
            IProductReferenceCollectorV3 productReferenceCollector,
            IProductReferenceResolverV3 productReferenceResolver,
            IConversationContextServiceV3 conversationContextService,
            IKernelChatServiceV3 kernelChatService,
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
            SendMessageCommandV3 request,
            CancellationToken cancellationToken)
        {
            // Keep the original business and quota checks before creating messages.
            var (business, businessCurrentQuota) = await BusinessValidation();


            if (!business.IsSuccess || business.Data is null || businessCurrentQuota is null)
            {
                return Result<ConversationResponse>
                    .Failure(business.StatusCode, business.Message, null, business.MessageCode);
            }

            var customer = await GetOrCreateCustomerAsync(request.ExternalCustomerId, business.Data!);

            Conversation? conversation;
            try
            {
                var createTime = _time.GetUtcNow();

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

                var sw = Stopwatch.StartNew();

                var conversationContext = await _conversationContextService.GetOrLoadAsyncConversationCache(
                    conversation.Id.ToString(), cancellationToken);


                KernelChatRequestV3 req = new()
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

                var productById = BuildAvailableProductReferences(
                    cacheProducts,
                    conversationContext);

                var selectedProductIds = kernelResult.SelectedProductIds
                    .Where(productId => !string.IsNullOrWhiteSpace(productId))
                    .Select(productId => productId.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Use the same reference type through tools, selection, and conversation turns.
                var responseProductIds = selectedProductIds.Where(productById.ContainsKey).ToList();
                var responseProductById = await _productReferenceResolver.ResolveAsync(
                    business.Data.Id, responseProductIds, cacheProducts, cancellationToken);
                var selectedProductReferences = _productReferenceResolver
                    .GetInOrder(responseProductIds, responseProductById)
                    .Select((product, index) =>
                    {
                        product.DisplayOrder = index + 1;
                        product.ExternalProductId = NormalizeExternalProductId(product.ExternalProductId);
                        return product;
                    }).ToList();
                var productListResponse = selectedProductReferences
                    .Select(product => product.ToMessageResponse()).ToList();

                // Only the persistence boundary uses the existing stored reference format.
                aiMessage.CacheProductReference = selectedProductReferences.Select(product => new ProductReference
                {
                    ProductId = product.ProductId,
                    ExternalProductId = product.ExternalProductId,
                    DisplayName = product.Name
                }).ToList();

                // Preserve the original chat credit formula and quota updates.
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

                // Publish the same events after the original transaction has committed.
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

                var turn = new CachedConversationTurnV3
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


                // Keep the same configured window and cache save order as the original handler.
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
                    .Failure(500, "Error when saving user message or kernel response", null, "MG_SERVER_500");
            }

        }

        private static Dictionary<string, ProductReferenceV3> BuildAvailableProductReferences(
            IEnumerable<ProductReferenceV3> currentProducts,
            ConversationContextCacheV3 conversationContext)
        {
            var products = new Dictionary<string, ProductReferenceV3>(StringComparer.OrdinalIgnoreCase);
            foreach (var product in conversationContext.RecentTurns
                .SelectMany(turn => turn.AssistantMessage?.ProductReferences ?? []).Concat(currentProducts))
            {
                if (!string.IsNullOrWhiteSpace(product.ProductId))
                    products[product.ProductId.Trim()] = product.Copy();
            }
            return products;
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
            KernelChatResultV3 kernelResult,
            IReadOnlyCollection<ProductReferenceV3> retrievedProducts,
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
                retrievedProducts,
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


        // Detect the Markdown separator row used by the existing comparison event logic.
        private static int FindMarkdownTableHeaderIndex(string content)
        {
            var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

            for (var index = 0; index < lines.Length - 1; index++)
            {
                if (lines[index].Count(character => character == '|') < 2)
                {
                    continue;
                }

                var separatorCells = lines[index + 1]
                    .Trim()
                    .Trim('|')
                    .Split('|', StringSplitOptions.TrimEntries);

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

        private async Task<(Result<Business>, BusinessQuota? businessQuota)> BusinessValidation()
        {
            var business = await _currentUserService.GetBusiness();

            if (!business.IsSuccess || business.Data is null)
                return (
                    Result<Business>.Failure(
                    statusCode: business.StatusCode,
                    message: business.Message,
                    messageCode: business.MessageCode),
                    null);

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
