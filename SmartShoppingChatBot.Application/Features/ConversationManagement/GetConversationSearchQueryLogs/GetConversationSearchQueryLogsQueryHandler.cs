using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationSearchQueryLogs;

public sealed class GetConversationSearchQueryLogsQueryHandler(
    ICurrentUserService currentUserService,
    ICustomerRepository customerRepository,
    IConversationRepository conversationRepository,
    ISearchQueryLogRepository searchQueryLogRepository)
    : IRequestHandler<GetConversationSearchQueryLogsQuery, Result<CursorPage<SearchQueryLogResponse>>>
{
    public async Task<Result<CursorPage<SearchQueryLogResponse>>> Handle(
        GetConversationSearchQueryLogsQuery request,
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
                return Failure(400, "Invalid search query log cursor.", MessageCodeForMessage.InvalidCursor);
            }

            lastCursor = parsedCursor;
        }

        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<CursorPage<SearchQueryLogResponse>>.Failure(
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

        var page = await searchQueryLogRepository.CursorPagingAsync(
            businessId,
            conversationId,
            request.Filter.Limit,
            lastCursor,
            cancellationToken);

        return Result<CursorPage<SearchQueryLogResponse>>.Success(
            new CursorPage<SearchQueryLogResponse>
            {
                Items = page.Items.Select(SearchQueryLogResponse.FromEntity).ToList(),
                HasMore = page.HasMore,
                NextCursor = page.NextCursor
            },
            200,
            "Get conversation search query logs successfully.",
            ConversationMessageCode.Success);
    }

    private static Result<CursorPage<SearchQueryLogResponse>> Failure(
        int statusCode,
        string message,
        string messageCode) => Result<CursorPage<SearchQueryLogResponse>>.Failure(
            statusCode,
            message,
            messageCode: messageCode);
}
