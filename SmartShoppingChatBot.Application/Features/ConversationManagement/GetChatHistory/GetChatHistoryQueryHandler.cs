using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory
{
    public class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, Result<CursorPage<ConversationMessageResponse>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ICustomerRepository _customerRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IProductReferenceResolver _productReferenceResolver;

        public GetChatHistoryQueryHandler(
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
            GetChatHistoryQuery request,
            CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(request.ConversationId, out var conversationId))
            {
                return EmptySuccess();
            }

            ObjectId? lastCursor = null;
            if (!string.IsNullOrWhiteSpace(request.LastCursor))
            {
                if (!ObjectId.TryParse(request.LastCursor, out var parsedCursor))
                {
                    return EmptySuccess();
                }

                lastCursor = parsedCursor;
            }

            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess)
            {
                return Result<CursorPage<ConversationMessageResponse>>.Failure(
                    statusCode: business.StatusCode,
                    message: business.MessageCode,
                    errors: business.Errors,
                    messageCode: business.MessageCode);
            }

            var businessId = business.Data!.Id;
            var customer = await _customerRepository.FindAsync(x =>
                x.CustomerExternalId == request.ExternalCustomerId
                && x.BusinessId == businessId);

            if (customer == null)
            {
                return EmptySuccess();
            }

            var conversation = await _conversationRepository.FindAsync(x =>
                x.Id == conversationId
                && x.CustomerId == customer.Id
                && x.BusinessId == businessId);

            if (conversation is null)
            {
                return EmptySuccess();
            }

            var messages = await _messageRepository.MessageCursorPaging(
                conversationId,
                request.Limit,
                lastCursor);

            var productIds = messages.Items
                .SelectMany(message => message.CacheProductReference ?? [])
                .Select(product => product.ProductId);

            var productById = await _productReferenceResolver.ResolveAsync(
                businessId,
                productIds,
                cancellationToken: cancellationToken);

            var response = new CursorPage<ConversationMessageResponse>
            {
                Items = messages.Items.Select(x => new ConversationMessageResponse
                {
                    Id = x.Id.ToString(),
                    Content = x.Content,
                    SenderType = x.SenderType,
                    ContentType = x.ContentType,
                    CreatedAt = x.CreatedAt,
                    ProductReferences = _productReferenceResolver
                        .GetInOrder(
                            (x.CacheProductReference ?? []).Select(product => product.ProductId),
                            productById)
                        .Select(MessageProductResponse.FromProduct)
                        .ToList()
                }).ToList(),
                HasMore = messages.HasMore,
                NextCursor = messages.NextCursor
            };

            return Result<CursorPage<ConversationMessageResponse>>.Success(
                data: response,
                statusCode: 200,
                message: "Get chat history successfully",
                messageCode: MessageCodeForMessage.Success);
        }

        private static Result<CursorPage<ConversationMessageResponse>> EmptySuccess()
        {
            return Result<CursorPage<ConversationMessageResponse>>.Success(
                data: new CursorPage<ConversationMessageResponse>(),
                statusCode: 200,
                message: "Get chat history successfully",
                messageCode: MessageCodeForMessage.Success);
        }
    }
}
