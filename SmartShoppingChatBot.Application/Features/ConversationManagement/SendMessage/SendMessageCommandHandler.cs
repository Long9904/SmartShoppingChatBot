using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
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
        private readonly IConversationContextService _conversationContextService;
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
            IConversationContextService conversationContextService,
            IKernelChatService kernelChatService)

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
            _conversationContextService = conversationContextService;
            _kernelChatService = kernelChatService;
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

                await _unitOfWork.BeginTransactionAsync(cancellationToken);

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
                    conversation = await _conversationRepository.FindAsync(x =>
                    x.Id == ObjectId.Parse(request.ConversationId));

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

                _logger.LogInformation($"{kernelResult.ComparedProductIds.ToArray()}");
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

                var productById = BuildAvailableProductReferences(
                    cacheProducts,
                    conversationContext);

                var selectedProductIds = kernelResult.SelectedProductIds
                    .Where(productId => !string.IsNullOrWhiteSpace(productId))
                    .Select(productId => productId.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var selectedProductReferences = selectedProductIds
                    .Where(productById.ContainsKey)
                    .Select((productId, index) =>
                    {
                        var product = productById[productId];

                        return new CachedProductReference
                        {
                            DisplayOrder = index + 1,
                            DisplayName = product.DisplayName,
                            ProductId = product.ProductId,
                        };
                    })
                    .ToList();

                aiMessage.CacheProductReference = selectedProductReferences
                    .Select(product =>
                    {
                        return new ProductReference
                        {
                            ProductId = product.ProductId,
                            DisplayName = product.DisplayName
                        };
                    })
                    .ToList();


                sw.Stop();
                var gptCredits = kernelResult.InputTokens + kernelResult.OutputTokens * 6;
                var usageLog = new UsageQuotaLog
                {
                    BillableTokens = gptCredits,
                    OutputTokens = kernelResult.OutputTokens,
                    InputTokens = kernelResult.InputTokens,
                    CreatedAt = DateTimeOffset.UtcNow,
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
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                // 5. Build turn cache to save orther context to redis
                var turnCreateTime = _time.GetUtcNow();

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
            IEnumerable<ProductResponseV2> currentProducts,
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
                    DisplayName = product.Name,
                };
            }

            return productById;
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
