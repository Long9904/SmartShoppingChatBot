using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;

public sealed class GetCustomerConversationDetailQueryHandler
    : IRequestHandler<GetCustomerConversationDetailQuery, Result<CursorPage<ConversationMessageResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IProductReferenceResolver _productReferenceResolver;

    public GetCustomerConversationDetailQueryHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IProductReferenceResolver productReferenceResolver)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _productReferenceResolver = productReferenceResolver;
    }

    public async Task<Result<CursorPage<ConversationMessageResponse>>> Handle(
        GetCustomerConversationDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.ConversationId, out var conversationId))
        {
            return Result<CursorPage<ConversationMessageResponse>>.Failure(
                400,
                "Invalid conversation ID.",
                messageCode: ConversationMessageCode.InvalidId);
        }

        ObjectId? lastCursor = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.LastCursor))
        {
            if (!ObjectId.TryParse(request.Filter.LastCursor, out var parsedCursor))
            {
                return Result<CursorPage<ConversationMessageResponse>>.Failure(
                    400,
                    "Invalid message cursor.",
                    messageCode: MessageCodeForMessage.InvalidCursor);
            }

            lastCursor = parsedCursor;
        }

        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<CursorPage<ConversationMessageResponse>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var businessId = businessResult.Data.Id;
        var customer = await _customerRepository.FindAsync(candidate =>
            candidate.BusinessId == businessId
            && candidate.CustomerExternalId == request.CustomerExternalId);

        if (customer is null)
        {
            return Result<CursorPage<ConversationMessageResponse>>.Failure(
                404,
                "Customer not found.",
                messageCode: CustomerMessageCode.NotFound);
        }

        var conversation = await _conversationRepository.FindAsync(candidate =>
            candidate.Id == conversationId
            && candidate.BusinessId == businessId
            && candidate.CustomerId == customer.Id);

        if (conversation is null)
        {
            return Result<CursorPage<ConversationMessageResponse>>.Failure(
                404,
                "Conversation not found.",
                messageCode: ConversationMessageCode.NotFound);
        }

        var filter = request.Filter;
        var messages = await _messageRepository.MessageCursorPaging(
            conversationId,
            filter.Limit,
            lastCursor,
            filter.Search?.Trim(),
            filter.SenderType);

        var productIds = messages.Items
            .SelectMany(message => message.CacheProductReference ?? [])
            .Select(product => product.ProductId);

        var productById = await _productReferenceResolver.ResolveAsync(
            businessId,
            productIds,
            cancellationToken: cancellationToken);

        var response = new CursorPage<ConversationMessageResponse>
        {
            Items = messages.Items
                .Select(message => MapMessage(message, productById))
                .ToList(),
            HasMore = messages.HasMore,
            NextCursor = messages.NextCursor
        };

        return Result<CursorPage<ConversationMessageResponse>>.Success(
            response,
            200,
            "Get chat history successfully.",
            MessageCodeForMessage.Success);
    }

    private ConversationMessageResponse MapMessage(
        Message message,
        IReadOnlyDictionary<string, ProductResponseV2> productById)
    {
        return new ConversationMessageResponse
        {
            Id = message.Id.ToString(),
            Content = message.Content,
            SenderType = message.SenderType,
            ContentType = message.ContentType,
            CreatedAt = message.CreatedAt,
            ProductReferences = _productReferenceResolver
                .GetInOrder(
                    (message.CacheProductReference ?? []).Select(product => product.ProductId),
                    productById)
                .Select(MessageProductResponse.FromProduct)
                .ToList()
        };
    }
}
