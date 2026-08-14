using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.ReceiveConversationOrderEvent;

public sealed class ReceiveConversationOrderEventCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository,
    IConversationOrderEventRepository orderEventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ReceiveConversationOrderEventCommand, Result<ConversationOrderEventResponse>>
{
    public async Task<Result<ConversationOrderEventResponse>> Handle(
        ReceiveConversationOrderEventCommand request,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.ConversationId, out var conversationId))
        {
            return Result<ConversationOrderEventResponse>.Failure(
                400,
                "Invalid conversation ID.",
                messageCode: ConversationMessageCode.InvalidId);
        }

        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<ConversationOrderEventResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var businessId = businessResult.Data.Id;
        var conversationExists = await conversationRepository.FindAsync(conversation =>
            conversation.Id == conversationId && conversation.BusinessId == businessId);
        if (conversationExists is null)
        {
            return Result<ConversationOrderEventResponse>.Failure(
                404,
                "Conversation not found.",
                messageCode: ConversationMessageCode.NotFound);
        }

        var entity = new ConversationOrderEvent
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = conversationId,
            ExternalOrderId = request.Event.ExternalOrderId?.Trim(),
            Status = request.Event.Status,
            Amount = request.Event.Amount,
            CreatedAt = timeProvider.GetUtcNow(),
            ProductOrderSnapshotItems = request.Event.Products.Select(product => new ProductOrderSnapshot
            {
                ExternalProductId = product.ExternalProductId.Trim(),
                ProductName = product.ProductName?.Trim(),
                Price = product.Price,
                Quantity = product.Quantity
            }).ToList()
        };

        await orderEventRepository.AddAsync(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ConversationOrderEventResponse>.Success(
            ConversationOrderEventResponse.FromEntity(entity),
            201,
            "Conversation order event received.",
            ConversationMessageCode.Success);
    }

}
