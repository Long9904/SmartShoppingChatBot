using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationOrderEvents;

public sealed class GetConversationOrderEventsQueryHandler(
    ICurrentUserService currentUserService,
    ICustomerRepository customerRepository,
    IConversationRepository conversationRepository,
    IConversationOrderEventRepository orderEventRepository)
    : IRequestHandler<GetConversationOrderEventsQuery, Result<CursorPage<ConversationOrderEventResponse>>>
{
    public async Task<Result<CursorPage<ConversationOrderEventResponse>>> Handle(
        GetConversationOrderEventsQuery request,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.ConversationId, out var conversationId))
        {
            return Failure(400, "Invalid conversation ID.", ConversationMessageCode.InvalidId);
        }

        ObjectId? lastCursor = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.LastCursor))
        {
            if (!ObjectId.TryParse(request.Filter.LastCursor, out var parsedCursor))
            {
                return Failure(400, "Invalid order event cursor.", MessageCodeForMessage.InvalidCursor);
            }

            lastCursor = parsedCursor;
        }

        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<CursorPage<ConversationOrderEventResponse>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var businessId = businessResult.Data.Id;
        var customer = await customerRepository.FindAsync(item =>
            item.BusinessId == businessId
            && item.CustomerExternalId == request.CustomerExternalId);
        if (customer is null)
        {
            return Failure(404, "Customer not found.", CustomerMessageCode.NotFound);
        }

        var conversation = await conversationRepository.FindAsync(item =>
            item.Id == conversationId
            && item.BusinessId == businessId
            && item.CustomerId == customer.Id);
        if (conversation is null)
        {
            return Failure(404, "Conversation not found.", ConversationMessageCode.NotFound);
        }

        var page = await orderEventRepository.CursorPagingAsync(
            businessId,
            conversationId,
            request.Filter.Limit,
            lastCursor,
            cancellationToken);

        return Result<CursorPage<ConversationOrderEventResponse>>.Success(
            new CursorPage<ConversationOrderEventResponse>
            {
                Items = page.Items.Select(ConversationOrderEventResponse.FromEntity).ToList(),
                HasMore = page.HasMore,
                NextCursor = page.NextCursor
            },
            200,
            "Get conversation order events successfully.",
            ConversationMessageCode.Success);
    }

    private static Result<CursorPage<ConversationOrderEventResponse>> Failure(
        int statusCode,
        string message,
        string messageCode) => Result<CursorPage<ConversationOrderEventResponse>>.Failure(
            statusCode,
            message,
            messageCode: messageCode);
}
